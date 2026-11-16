using Mailozaurr;
using Mailozaurr.MailboxExport;
using OfficeIMO.Email;
using OfficeIMO.Email.Store;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public sealed class GraphMailboxPstExportTests : IDisposable {
    private const string Mailbox = "user@contoso.com";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "mailozaurr-pst-" + Guid.NewGuid().ToString("N"));
    private readonly FakeGraphMailbox _graph = new FakeGraphMailbox();
    private readonly GraphCredential _credential = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
    private readonly HttpClient _client;
    private readonly FieldInfo _handlerField;
    private readonly HttpMessageHandler _originalHandler;
    private readonly string _tokenKey;
    private readonly ConcurrentDictionary<string, GraphAuthorization> _tokenCache;

    public GraphMailboxPstExportTests() {
        Directory.CreateDirectory(_directory);
        _client = (HttpClient)typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        _handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HttpClient handler field not found");
        _originalHandler = (HttpMessageHandler)_handlerField.GetValue(_client)!;
        _handlerField.SetValue(_client, _graph);
        _tokenCache = (ConcurrentDictionary<string, GraphAuthorization>)typeof(MicrosoftGraphUtils)
            .GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        _tokenKey = MicrosoftGraphUtils.BuildGraphTokenCacheKey(_credential, "tenant", "https://graph.microsoft.com");
        _tokenCache[_tokenKey] = new GraphAuthorization { AccessToken = "token", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddHours(1) };
    }

    public void Dispose() {
        _handlerField.SetValue(_client, _originalHandler);
        _tokenCache.TryRemove(_tokenKey, out _);
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task ExportsMailboxIntoVerifiedSizeLimitedPartsWithTypedItems() {
        _graph.FailingMimeIds.Add("inbox-2");
        _graph.ThrottledMimeIds.Add("sent-1");
        _graph.OversizedMimeIds.Add("projects-2");
        var options = NewOptions("archive.pst", GraphMailboxExportItemTypes.All);
        // Each message is about 300 bytes plus the default 16 KiB per-item overhead, so two items fit in a part.
        options.Split = new EmailStorePstSplitOptions(maxEstimatedBytesPerPart: 40 * 1024);

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.Equal(5, report.Written.Messages);
        Assert.Equal(1, report.Written.Contacts);
        Assert.Equal(2, report.Written.Events);
        Assert.Equal(1, report.Written.Tasks);
        Assert.Equal(1, report.ItemsFailed);
        Assert.Equal(1, report.ItemsSkipped);
        Assert.Contains(report.Issues, issue => issue.Kind == GraphMailboxExportIssueKind.Failed && issue.ItemId == "inbox-2");
        Assert.Contains(report.Issues, issue => issue.Kind == GraphMailboxExportIssueKind.Skipped && issue.ItemId == "projects-2");
        Assert.Contains(report.Issues, issue => issue.Kind == GraphMailboxExportIssueKind.Partial && issue.ItemId == "event-weekly");
        Assert.Equal(2, _graph.MimeRequests.Count(id => id == "sent-1"));

        Assert.True(report.Parts.Count >= 4);
        Assert.Equal(Enumerable.Range(1, report.Parts.Count), report.Parts.Select(part => part.Number));
        Assert.Equal(report.ItemsWritten, report.Parts.Sum(part => part.ItemCount));
        var documents = new List<(string Folder, EmailDocument Document)>();
        foreach (GraphMailboxPstExportPart part in report.Parts) {
            Assert.Equal(GraphMailboxPstExporter.GetPartPath(options.DestinationPath, part.Number), part.Path);
            Assert.True(part.Verified);
            Assert.True(part.EstimatedBytes <= 40 * 1024);
            Assert.Equal(new FileInfo(part.Path).Length, part.Bytes);
            Assert.Equal(Sha256(part.Path), part.Sha256);
            documents.AddRange(ReadPart(part.Path));
        }
        Assert.False(File.Exists(options.CheckpointPath));

        Assert.Equal(report.ItemsWritten, documents.Count);
        Assert.Equal(
            new[] { "Inbox", "Inbox\\Projects", "Sent Items", "Deleted Items" }.OrderBy(name => name),
            documents.Where(item => item.Document.OutlookItemKind == OutlookItemKind.Message)
                .Select(item => item.Folder).Distinct().OrderBy(name => name));
        EmailDocument firstInbox = documents.Single(item => item.Document.Subject == "Message inbox-1").Document;
        Assert.True(firstInbox.MessageMetadata.IsRead);
        Assert.Contains("Blue", firstInbox.MessageMetadata.Categories);

        EmailDocument contact = documents.Single(item => item.Document.OutlookItemKind == OutlookItemKind.Contact).Document;
        Assert.Equal("Ada Lovelace", contact.Contact!.DisplayName);
        Assert.Equal("ada@contoso.com", contact.Contact.Email1.Address);
        Assert.Equal("+1 555 0100", contact.Contact.Phones.Business);

        EmailDocument weekly = documents.Single(item => item.Document.Subject == "Weekly sync").Document;
        Assert.Equal(OutlookItemKind.Appointment, weekly.OutlookItemKind);
        Assert.NotNull(weekly.Appointment!.Recurrence);
        Assert.Equal(OutlookRecurrenceFrequency.Weekly, weekly.Appointment.Recurrence!.Frequency);
        Assert.Equal(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero), weekly.Appointment.Start);

        EmailDocument task = documents.Single(item => item.Document.OutlookItemKind == OutlookItemKind.Task).Document;
        Assert.Equal("File expenses", task.Subject);
        Assert.Equal(1, task.Task!.Status);
    }

    [Fact]
    public async Task ResumesInterruptedExportFromCheckpointWithoutDuplicates() {
        var options = NewOptions("resume.pst", GraphMailboxExportItemTypes.Mail);
        options.Split = new EmailStorePstSplitOptions(maxEstimatedBytesPerPart: 40 * 1024);
        options.CheckpointIntervalItems = 2;
        using (var cancellation = new CancellationTokenSource()) {
            // Stop while the third Inbox message downloads, so the resumed run continues inside a folder.
            _graph.OnMimeRequest = (id, count) => {
                if (id == "inbox-3") cancellation.Cancel();
            };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewExporter(options).ExportAsync(cancellation.Token));
        }
        Assert.True(File.Exists(options.CheckpointPath));
        _graph.OnMimeRequest = null;

        options.Resume = true;
        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.True(report.Resumed);
        Assert.Equal(7, report.Written.Messages);
        Assert.Equal(0, report.ItemsFailed);
        string[] subjects = report.Parts.SelectMany(part => ReadPart(part.Path)).Select(item => item.Document.Subject!).ToArray();
        Assert.Equal(7, subjects.Length);
        Assert.Equal(subjects.Length, subjects.Distinct().Count());
        Assert.False(File.Exists(options.CheckpointPath));
    }

    [Fact]
    public async Task AppliesFolderAndDateFiltersAtTheSource() {
        var options = NewOptions("filtered.pst", GraphMailboxExportItemTypes.Mail);
        options.IncludeFolders.Add("Inbox");
        options.ExcludeFolders.Add("inbox\\proj*");
        options.StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        options.EndDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.Equal(2, _graph.MessageListRequests.Count);
        Assert.All(_graph.MessageListRequests, request => {
            Assert.Contains("/mailFolders/inbox-id/messages", request);
            Assert.Contains("receivedDateTime ge 2026-01-01T00:00:00Z and receivedDateTime lt 2026-02-01T00:00:00Z", request);
            Assert.Contains("$orderby=receivedDateTime asc", request);
        });
        Assert.Equal(3, report.Written.Messages);
        GraphMailboxPstExportPart part = Assert.Single(report.Parts);
        Assert.Equal(options.DestinationPath, part.Path);
        Assert.All(ReadPart(part.Path), item => Assert.Equal("Inbox", item.Folder));
    }

    private GraphMailboxPstExportOptions NewOptions(string fileName, GraphMailboxExportItemTypes types) {
        string destination = Path.Combine(_directory, fileName);
        return new GraphMailboxPstExportOptions {
            Mailbox = Mailbox,
            DestinationPath = destination,
            ItemTypes = types,
            CheckpointPath = Path.ChangeExtension(destination, ".checkpoint"),
            MaxItemBytes = 4096
        };
    }

    private GraphMailboxPstExporter NewExporter(GraphMailboxPstExportOptions options) =>
        new GraphMailboxPstExporter(
            new GraphMailboxExportSource(_credential, Mailbox, (_, _) => Task.CompletedTask),
            options);

    private static List<(string Folder, EmailDocument Document)> ReadPart(string path) {
        using EmailStoreSession session = EmailStoreSession.Open(path);
        Dictionary<string, EmailStoreFolderInfo> folders = session.Folders.ToDictionary(folder => folder.Id);
        var result = new List<(string, EmailDocument)>();
        foreach (EmailStoreItemReference reference in session.EnumerateItems().ToList()) {
            result.Add((FolderPath(folders, reference.FolderId), session.ReadItem(reference).Document));
        }
        return result;
    }

    private static string FolderPath(Dictionary<string, EmailStoreFolderInfo> folders, string id) {
        var names = new List<string>();
        for (EmailStoreFolderInfo? folder = folders[id]; folder != null;
            folder = folder.ParentId != null && folders.TryGetValue(folder.ParentId, out var parent) ? parent : null) {
            if (folder.SpecialFolderKind is EmailStoreSpecialFolderKind.Root or EmailStoreSpecialFolderKind.IpmSubtree) break;
            names.Insert(0, folder.Name);
        }
        return string.Join("\\", names);
    }

    private static string Sha256(string path) {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("X2")));
    }

    /// <summary>Serves a small mailbox through the Microsoft Graph v1.0 resource shapes the exporter reads.</summary>
    private sealed class FakeGraphMailbox : HttpMessageHandler {
        private const string Root = "https://graph.microsoft.com/v1.0/users/user%40contoso.com/";
        private int _mimeCount;

        internal HashSet<string> FailingMimeIds { get; } = new HashSet<string>();
        internal HashSet<string> ThrottledMimeIds { get; } = new HashSet<string>();
        internal HashSet<string> OversizedMimeIds { get; } = new HashSet<string>();
        internal ConcurrentQueue<string> MimeRequests { get; } = new ConcurrentQueue<string>();
        internal ConcurrentQueue<string> MessageListRequests { get; } = new ConcurrentQueue<string>();
        internal Action<string, int>? OnMimeRequest { get; set; }

        private static readonly Dictionary<string, (string Id, DateTimeOffset Received)[]> Messages =
            new Dictionary<string, (string, DateTimeOffset)[]> {
                ["inbox-id"] = new[] {
                    ("inbox-1", new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero)),
                    ("inbox-2", new DateTimeOffset(2026, 1, 6, 8, 0, 0, TimeSpan.Zero)),
                    ("inbox-3", new DateTimeOffset(2026, 1, 7, 8, 0, 0, TimeSpan.Zero))
                },
                ["projects-id"] = new[] {
                    ("projects-1", new DateTimeOffset(2026, 1, 8, 8, 0, 0, TimeSpan.Zero)),
                    ("projects-2", new DateTimeOffset(2026, 1, 9, 8, 0, 0, TimeSpan.Zero))
                },
                ["sent-id"] = new[] {
                    ("sent-1", new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero))
                },
                ["deleted-id"] = new[] {
                    ("deleted-1", new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero))
                }
            };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            string uri = request.RequestUri!.AbsoluteUri;
            Assert.StartsWith(Root, uri);
            string path = request.RequestUri.AbsolutePath.Substring("/v1.0/users/user%40contoso.com/".Length);
            string query = Uri.UnescapeDataString(request.RequestUri.Query);
            return Task.FromResult(Route(path, query, uri, cancellationToken));
        }

        private HttpResponseMessage Route(string path, string query, string uri, CancellationToken cancellationToken) {
            switch (path) {
                case "mailFolders/inbox": return Json("{\"id\":\"inbox-id\"}");
                case "mailFolders/sentitems": return Json("{\"id\":\"sent-id\"}");
                case "mailFolders/deleteditems": return Json("{\"id\":\"deleted-id\"}");
                case "mailFolders/drafts":
                case "mailFolders/junkemail":
                case "mailFolders/outbox":
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":{\"code\":\"ErrorItemNotFound\"}}") };
                case "mailFolders":
                    return Json("{\"value\":[" +
                        Folder("inbox-id", "Inbox", 3, 1) + "," +
                        Folder("sent-id", "Sent Items", 1, 0) + "," +
                        Folder("deleted-id", "Deleted Items", 1, 0) + "]}");
                case "mailFolders/inbox-id/childFolders":
                    return Json("{\"value\":[" + Folder("projects-id", "Projects", 2, 0) + "]}");
                case "contactFolders":
                    return Json("{\"value\":[]}");
                case "contacts":
                    return Json("{\"value\":[{\"id\":\"contact-1\",\"displayName\":\"Ada Lovelace\",\"givenName\":\"Ada\",\"surname\":\"Lovelace\"," +
                        "\"emailAddresses\":[{\"name\":\"Ada Lovelace\",\"address\":\"ada@contoso.com\"}],\"businessPhones\":[\"+1 555 0100\"]," +
                        "\"companyName\":\"Analytical Engines\"}]}");
                case "calendars":
                    return Json("{\"value\":[{\"id\":\"calendar-id\",\"name\":\"Calendar\",\"isDefaultCalendar\":true}]}");
                case "calendars/calendar-id/events":
                    return Json("{\"value\":[" +
                        "{\"id\":\"event-single\",\"subject\":\"Design review\",\"start\":{\"dateTime\":\"2026-03-03T13:00:00.0000000\",\"timeZone\":\"UTC\"}," +
                        "\"end\":{\"dateTime\":\"2026-03-03T14:00:00.0000000\",\"timeZone\":\"UTC\"},\"showAs\":\"busy\"," +
                        "\"attendees\":[{\"type\":\"required\",\"emailAddress\":{\"name\":\"Ada\",\"address\":\"ada@contoso.com\"}}]}," +
                        "{\"id\":\"event-weekly\",\"subject\":\"Weekly sync\",\"type\":\"seriesMaster\",\"start\":{\"dateTime\":\"2026-03-02T09:00:00.0000000\",\"timeZone\":\"UTC\"}," +
                        "\"end\":{\"dateTime\":\"2026-03-02T09:30:00.0000000\",\"timeZone\":\"UTC\"},\"originalStartTimeZone\":\"UTC\"," +
                        "\"recurrence\":{\"pattern\":{\"type\":\"weekly\",\"interval\":1,\"daysOfWeek\":[\"monday\"],\"firstDayOfWeek\":\"monday\"}," +
                        "\"range\":{\"type\":\"numbered\",\"startDate\":\"2026-03-02\",\"numberOfOccurrences\":10,\"recurrenceTimeZone\":\"UTC\"}}}]}");
                case "todo/lists":
                    return Json("{\"value\":[{\"id\":\"tasks-id\",\"displayName\":\"Tasks\",\"wellknownListName\":\"defaultList\"}]}");
                case "todo/lists/tasks-id/tasks":
                    return Json("{\"value\":[{\"id\":\"task-1\",\"title\":\"File expenses\",\"status\":\"inProgress\",\"importance\":\"high\"," +
                        "\"dueDateTime\":{\"dateTime\":\"2026-03-10T00:00:00.0000000\",\"timeZone\":\"UTC\"}}]}");
            }
            if (path.StartsWith("mailFolders/", StringComparison.Ordinal) && path.EndsWith("/messages", StringComparison.Ordinal)) {
                MessageListRequests.Enqueue(Uri.UnescapeDataString(uri));
                return MessagePage(path.Split('/')[1], query, uri);
            }
            if (path.StartsWith("messages/", StringComparison.Ordinal) && path.EndsWith("/$value", StringComparison.Ordinal)) {
                return Mime(path.Split('/')[1], cancellationToken);
            }
            throw new InvalidOperationException("Unexpected Graph request: " + uri);
        }

        private HttpResponseMessage MessagePage(string folderId, string query, string uri) {
            IEnumerable<(string Id, DateTimeOffset Received)> messages = Messages[folderId];
            const string from = "receivedDateTime ge ";
            int start = query.IndexOf(from, StringComparison.Ordinal);
            if (start >= 0) {
                DateTimeOffset bound = DateTimeOffset.Parse(query.Substring(start + from.Length, 20), System.Globalization.CultureInfo.InvariantCulture);
                messages = messages.Where(message => message.Received >= bound);
            }
            // Two messages per page so paging continues through @odata.nextLink.
            int skip = query.Contains("$skip=2") ? 2 : 0;
            var page = messages.Skip(skip).Take(2).ToArray();
            string? next = skip == 0 && messages.Count() > 2 ? uri + "&$skip=2" : null;
            var body = new StringBuilder("{\"value\":[");
            body.Append(string.Join(",", page.Select(message =>
                "{\"id\":\"" + message.Id + "\",\"subject\":\"Message " + message.Id + "\",\"receivedDateTime\":\"" +
                message.Received.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",\"isRead\":true,\"isDraft\":false," +
                "\"importance\":\"normal\",\"categories\":[\"Blue\"]}")));
            body.Append(']');
            if (next != null) body.Append(",\"@odata.nextLink\":\"").Append(next).Append('"');
            body.Append('}');
            return Json(body.ToString());
        }

        private HttpResponseMessage Mime(string id, CancellationToken cancellationToken) {
            MimeRequests.Enqueue(id);
            int count = Interlocked.Increment(ref _mimeCount);
            OnMimeRequest?.Invoke(id, count);
            cancellationToken.ThrowIfCancellationRequested();
            if (FailingMimeIds.Contains(id)) {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{\"error\":{\"code\":\"ErrorInternalServerError\"}}") };
            }
            if (ThrottledMimeIds.Remove(id)) {
                var throttled = new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("{\"error\":{\"code\":\"TooManyRequests\"}}") };
                throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return throttled;
            }
            string padding = OversizedMimeIds.Contains(id) ? new string('x', 8192) : "Body of " + id;
            string mime =
                "From: Sender <sender@contoso.com>\r\n" +
                "To: User <user@contoso.com>\r\n" +
                "Subject: Message " + id + "\r\n" +
                "Message-ID: <" + id + "@contoso.com>\r\n" +
                "Date: Mon, 05 Jan 2026 08:00:00 +0000\r\n" +
                "MIME-Version: 1.0\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
                padding + "\r\n";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(mime)) };
        }

        private static string Folder(string id, string name, int items, int children) =>
            "{\"id\":\"" + id + "\",\"displayName\":\"" + name + "\",\"totalItemCount\":" + items + ",\"childFolderCount\":" + children + "}";

        private static HttpResponseMessage Json(string body) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
