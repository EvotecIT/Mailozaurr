using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Mailozaurr.MailboxExport;

/// <summary>Reads an Exchange Online mailbox through Microsoft Graph for PST export.</summary>
/// <remarks>
/// Requests use the v1.0 endpoint. Throttled (429) and transient (502, 503, 504) responses are retried
/// with the server's Retry-After delay or an exponential backoff.
/// </remarks>
public sealed class GraphMailboxExportSource : IGraphMailboxExportSource {
    private const int PageSize = 100;
    private const string DefaultContactFolderId = "contacts";
    private const int MaxAttempts = 6;
    private static readonly string[] MessageFields = {
        "id", "receivedDateTime", "subject", "isRead", "isDraft", "importance", "categories"
    };
    private static readonly (string WellKnownName, GraphExportFolderRole Role)[] WellKnownMailFolders = {
        ("inbox", GraphExportFolderRole.Inbox),
        ("sentitems", GraphExportFolderRole.SentItems),
        ("drafts", GraphExportFolderRole.Drafts),
        ("deleteditems", GraphExportFolderRole.DeletedItems),
        ("junkemail", GraphExportFolderRole.JunkEmail),
        ("outbox", GraphExportFolderRole.Outbox)
    };

    private readonly GraphCredential _credential;
    private readonly string _mailbox;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>Creates a source for one mailbox.</summary>
    /// <param name="credential">Graph application or delegated credential.</param>
    /// <param name="mailbox">User principal name, SMTP address or object id of the mailbox.</param>
    public GraphMailboxExportSource(GraphCredential credential, string mailbox)
        : this(credential, mailbox, (delay, token) => Task.Delay(delay, token)) {
    }

    internal GraphMailboxExportSource(GraphCredential credential, string mailbox,
        Func<TimeSpan, CancellationToken, Task> delay) {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        if (string.IsNullOrWhiteSpace(mailbox)) throw new ArgumentException("A mailbox is required.", nameof(mailbox));
        _mailbox = mailbox;
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphExportFolder>> GetMailFoldersAsync(CancellationToken cancellationToken) {
        var roles = new Dictionary<string, GraphExportFolderRole>(StringComparer.Ordinal);
        foreach (var (wellKnownName, role) in WellKnownMailFolders) {
            string? id = await TryGetIdAsync(
                MailboxPath("mailFolders", wellKnownName) + "?$select=id", cancellationToken).ConfigureAwait(false);
            if (id != null && !roles.ContainsKey(id)) roles[id] = role;
        }

        var result = new List<GraphExportFolder>();
        var pending = new Queue<(string Uri, string? ParentId)>();
        pending.Enqueue((MailboxUri(FolderQuery(), "mailFolders"), null));
        while (pending.Count > 0) {
            var (uri, parentId) = pending.Dequeue();
            string? link = uri;
            while (link != null) {
                using JsonDocument page = await GetJsonAsync(link, cancellationToken).ConfigureAwait(false);
                foreach (JsonElement folder in Values(page)) {
                    string? id = GetString(folder, "id");
                    if (id == null) continue;
                    roles.TryGetValue(id, out GraphExportFolderRole role);
                    result.Add(new GraphExportFolder(
                        id,
                        GetString(folder, "displayName") ?? id,
                        parentId,
                        role,
                        GetInt64(folder, "totalItemCount")));
                    if ((GetInt64(folder, "childFolderCount") ?? 0) > 0) {
                        pending.Enqueue((MailboxUri(FolderQuery(), "mailFolders", id, "childFolders"), id));
                    }
                }
                link = NextLink(page);
            }
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<GraphExportPage<GraphExportMessageInfo>> GetMessagesAsync(
        string folderId,
        DateTimeOffset? receivedFrom,
        DateTimeOffset? receivedBefore,
        string? pageLink,
        CancellationToken cancellationToken) {
        string uri = pageLink ?? BuildMessagesUri(folderId, receivedFrom, receivedBefore);
        using JsonDocument page = await GetJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        var items = new List<GraphExportMessageInfo>();
        foreach (JsonElement message in Values(page)) {
            string? id = GetString(message, "id");
            if (id == null) continue;
            items.Add(new GraphExportMessageInfo(
                id,
                GetDateTimeOffset(message, "receivedDateTime"),
                GetString(message, "subject"),
                GetBoolean(message, "isRead"),
                GetBoolean(message, "isDraft"),
                GetString(message, "importance"),
                GetStrings(message, "categories")));
        }
        return new GraphExportPage<GraphExportMessageInfo>(items, NextLink(page));
    }

    /// <inheritdoc />
    public Task<byte[]> GetMessageMimeAsync(string messageId, long maxBytes, CancellationToken cancellationToken) =>
        WithRetryAsync(
            () => MicrosoftGraphUtils.GetMailMessageMimeBytesAsync(_credential, _mailbox, messageId, maxBytes, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphExportFolder>> GetContactFoldersAsync(CancellationToken cancellationToken) {
        var result = new List<GraphExportFolder> {
            new GraphExportFolder(DefaultContactFolderId, "Contacts", null, GraphExportFolderRole.Contacts)
        };
        var pending = new Queue<(string Uri, string ParentId)>();
        pending.Enqueue((MailboxUri(SelectQuery("id,displayName"), "contactFolders"), DefaultContactFolderId));
        while (pending.Count > 0) {
            var (uri, parentId) = pending.Dequeue();
            string? link = uri;
            while (link != null) {
                using JsonDocument page = await GetJsonAsync(link, cancellationToken).ConfigureAwait(false);
                foreach (JsonElement folder in Values(page)) {
                    string? id = GetString(folder, "id");
                    if (id == null) continue;
                    result.Add(new GraphExportFolder(id, GetString(folder, "displayName") ?? id, parentId));
                    pending.Enqueue((MailboxUri(SelectQuery("id,displayName"), "contactFolders", id, "childFolders"), id));
                }
                link = NextLink(page);
            }
        }
        return result;
    }

    /// <inheritdoc />
    public Task<GraphExportPage<JsonElement>> GetContactsAsync(string folderId, string? pageLink, CancellationToken cancellationToken) =>
        GetResourcePageAsync(
            pageLink ?? (string.Equals(folderId, DefaultContactFolderId, StringComparison.Ordinal)
                ? MailboxUri(TopQuery(), "contacts")
                : MailboxUri(TopQuery(), "contactFolders", folderId, "contacts")),
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphExportFolder>> GetCalendarsAsync(CancellationToken cancellationToken) {
        var result = new List<GraphExportFolder>();
        string? link = MailboxUri(SelectQuery("id,name,isDefaultCalendar"), "calendars");
        while (link != null) {
            using JsonDocument page = await GetJsonAsync(link, cancellationToken).ConfigureAwait(false);
            foreach (JsonElement calendar in Values(page)) {
                string? id = GetString(calendar, "id");
                if (id == null) continue;
                result.Add(new GraphExportFolder(
                    id,
                    GetString(calendar, "name") ?? id,
                    null,
                    GetBoolean(calendar, "isDefaultCalendar") == true ? GraphExportFolderRole.Calendar : GraphExportFolderRole.None));
            }
            link = NextLink(page);
        }
        return result;
    }

    /// <inheritdoc />
    public Task<GraphExportPage<JsonElement>> GetEventsAsync(string calendarId, string? pageLink, CancellationToken cancellationToken) =>
        GetResourcePageAsync(pageLink ?? MailboxUri(TopQuery(), "calendars", calendarId, "events"), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphExportFolder>> GetTaskListsAsync(CancellationToken cancellationToken) {
        var result = new List<GraphExportFolder>();
        string? link = MailboxUri(null, "todo", "lists");
        while (link != null) {
            using JsonDocument page = await GetJsonAsync(link, cancellationToken).ConfigureAwait(false);
            foreach (JsonElement list in Values(page)) {
                string? id = GetString(list, "id");
                if (id == null) continue;
                bool isDefault = string.Equals(GetString(list, "wellknownListName"), "defaultList", StringComparison.OrdinalIgnoreCase);
                result.Add(new GraphExportFolder(
                    id,
                    GetString(list, "displayName") ?? id,
                    null,
                    isDefault ? GraphExportFolderRole.Tasks : GraphExportFolderRole.None));
            }
            link = NextLink(page);
        }
        return result;
    }

    /// <inheritdoc />
    public Task<GraphExportPage<JsonElement>> GetTasksAsync(string listId, string? pageLink, CancellationToken cancellationToken) =>
        GetResourcePageAsync(pageLink ?? MailboxUri(TopQuery(), "todo", "lists", listId, "tasks"), cancellationToken);

    internal string BuildMessagesUri(string folderId, DateTimeOffset? receivedFrom, DateTimeOffset? receivedBefore) {
        var query = new Dictionary<string, object> {
            ["$select"] = string.Join(",", MessageFields),
            ["$orderby"] = "receivedDateTime asc",
            ["$top"] = PageSize
        };
        var filters = new List<string>();
        if (receivedFrom.HasValue) filters.Add("receivedDateTime ge " + FormatFilterDate(receivedFrom.Value));
        if (receivedBefore.HasValue) filters.Add("receivedDateTime lt " + FormatFilterDate(receivedBefore.Value));
        if (filters.Count > 0) query["$filter"] = string.Join(" and ", filters);
        return MailboxUri(query, "mailFolders", folderId, "messages");
    }

    private static string FormatFilterDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private async Task<GraphExportPage<JsonElement>> GetResourcePageAsync(string uri, CancellationToken cancellationToken) {
        using JsonDocument page = await GetJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        var items = new List<JsonElement>();
        foreach (JsonElement item in Values(page)) items.Add(item.Clone());
        return new GraphExportPage<JsonElement>(items, NextLink(page));
    }

    private async Task<string?> TryGetIdAsync(string relativeUri, CancellationToken cancellationToken) {
        try {
            using JsonDocument document = await GetJsonAsync(
                MicrosoftGraphUtils.JoinUriQuery(GraphEndpoint.V1, relativeUri), cancellationToken).ConfigureAwait(false);
            return GetString(document.RootElement, "id");
        } catch (GraphApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound) {
            return null;
        }
    }

    private Task<JsonDocument> GetJsonAsync(string uri, CancellationToken cancellationToken) =>
        WithRetryAsync(async () => {
            string token = await MicrosoftGraphUtils.ConnectO365GraphAsync(
                _credential, _credential.DirectoryId, "https://graph.microsoft.com", cancellationToken).ConfigureAwait(false);
            var headers = new Dictionary<string, string> { ["Authorization"] = token };
            return await MicrosoftGraphUtils.InvokeGraphApiAsync("GET", uri, headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken) {
        for (int attempt = 1; ; attempt++) {
            try {
                return await operation().ConfigureAwait(false);
            } catch (GraphApiException exception) when (attempt < MaxAttempts && IsTransient(exception.StatusCode)) {
                await _delay(GetRetryDelay(exception, attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static bool IsTransient(HttpStatusCode statusCode) =>
        (int)statusCode == 429 ||
        statusCode == HttpStatusCode.BadGateway ||
        statusCode == HttpStatusCode.ServiceUnavailable ||
        statusCode == HttpStatusCode.GatewayTimeout;

    private static TimeSpan GetRetryDelay(GraphApiException exception, int attempt) {
        if (exception.RetryAfter is TimeSpan retryAfter && retryAfter > TimeSpan.Zero) {
            return retryAfter > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : retryAfter;
        }
        return TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, attempt)));
    }

    private string MailboxPath(params string[] segments) {
        var all = new string[segments.Length + 2];
        all[0] = "users";
        all[1] = _mailbox;
        Array.Copy(segments, 0, all, 2, segments.Length);
        return MicrosoftGraphUtils.BuildGraphPath(all);
    }

    private string MailboxUri(IDictionary<string, object>? query, params string[] segments) =>
        MicrosoftGraphUtils.JoinUriQuery(GraphEndpoint.V1, MailboxPath(segments), query);

    private static Dictionary<string, object> FolderQuery() => new Dictionary<string, object> {
        ["$select"] = "id,displayName,parentFolderId,totalItemCount,childFolderCount",
        ["$top"] = PageSize
    };

    private static Dictionary<string, object> SelectQuery(string fields) => new Dictionary<string, object> {
        ["$select"] = fields,
        ["$top"] = PageSize
    };

    private static Dictionary<string, object> TopQuery() => new Dictionary<string, object> { ["$top"] = PageSize };

    private static IEnumerable<JsonElement> Values(JsonDocument page) =>
        page.RootElement.TryGetProperty("value", out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

    private static string? NextLink(JsonDocument page) =>
        page.RootElement.TryGetProperty("@odata.nextLink", out JsonElement link) && link.ValueKind == JsonValueKind.String
            ? link.GetString()
            : null;

    internal static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal static bool? GetBoolean(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false : null
            : null;

    internal static long? GetInt64(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out long number)
            ? number
            : null;

    internal static DateTimeOffset? GetDateTimeOffset(JsonElement element, string name) {
        string? text = GetString(element, name);
        return text != null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset value)
            ? value
            : null;
    }

    internal static IReadOnlyList<string> GetStrings(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array) {
            return Array.Empty<string>();
        }
        var result = new List<string>();
        foreach (JsonElement item in value.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) {
                result.Add(item.GetString()!);
            }
        }
        return result;
    }
}
