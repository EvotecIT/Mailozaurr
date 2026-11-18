using Mailozaurr;
using Mailozaurr.MailboxExport;
using OfficeIMO.Email;
using OfficeIMO.Email.Store;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        // Part 1 is complete, and part 2 contains the third Inbox message when the child folder download is interrupted.
        await InterruptExportAsync(options, "projects-1");
        Assert.True(File.Exists(GraphMailboxPstExporter.GetPartPath(options.DestinationPath, 1)));

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

    [Fact]
    public async Task FractionalMessageBoundsPreserveInclusiveStartAndExclusiveEnd() {
        var instant = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);
        _graph.Messages["inbox-id"] = new[] {
            ("before-start", instant.AddTicks(1_000_000)),
            ("at-start", instant.AddTicks(1_000_001)),
            ("inside-window", instant.AddTicks(2_000_002)),
            ("at-end", instant.AddTicks(3_000_003))
        };
        var options = NewOptions("fractional-bounds.pst", GraphMailboxExportItemTypes.Mail);
        options.IncludeFolders.Add("Inbox");
        options.ExcludeFolders.Add("Inbox\\Projects");
        options.StartDate = instant.AddTicks(1_000_001);
        options.EndDate = instant.AddTicks(3_000_003);

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        string request = Assert.Single(_graph.MessageListRequests);
        Assert.Contains("receivedDateTime ge 2026-01-05T08:00:00.1000001Z and receivedDateTime lt 2026-01-05T08:00:00.3000003Z", request);
        Assert.Equal(2, report.Written.Messages);
        Assert.Equal(new[] { "Message at-start", "Message inside-window" }, report.Parts.SelectMany(part => ReadPart(part.Path)).Select(item => item.Document.Subject).OrderBy(subject => subject));
    }

    [Fact]
    public async Task FractionalCheckpointResumesInsideOneSecondWithoutRewritingEarlierMessages() {
        var instant = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);
        _graph.Messages["inbox-id"] = new[] {
            ("inbox-1", instant.AddTicks(1_000_001)),
            ("inbox-2", instant.AddTicks(2_000_002)),
            ("inbox-3", instant.AddTicks(3_000_003))
        };
        var options = NewOptions("fractional-resume.pst", GraphMailboxExportItemTypes.Mail);
        options.IncludeFolders.Add("Inbox");
        options.ExcludeFolders.Add("Inbox\\Projects");
        options.CheckpointIntervalItems = 2;
        await InterruptExportAsync(options, "inbox-3");
        int previousRequests = _graph.MessageListRequests.Count;
        options.Resume = true;

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.True(report.Resumed);
        Assert.Equal(3, report.Written.Messages);
        Assert.Contains("receivedDateTime ge 2026-01-05T08:00:00.2000002Z", _graph.MessageListRequests.Skip(previousRequests).First());
        Assert.Equal(new[] { "Message inbox-1", "Message inbox-2", "Message inbox-3" }, report.Parts.SelectMany(part => ReadPart(part.Path)).Select(item => item.Document.Subject).OrderBy(subject => subject));
    }

    [Fact]
    public async Task DiscoversHiddenRootAndChildFoldersAndExportsTheirMessages() {
        _graph.IncludeHiddenFixture = true;
        var options = NewOptions("hidden.pst", GraphMailboxExportItemTypes.Mail);

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.Equal(2, _graph.FolderListRequests.Count);
        Assert.All(_graph.FolderListRequests, request => Assert.Contains("includeHiddenFolders=true", request, StringComparison.OrdinalIgnoreCase));
        var exported = report.Parts.SelectMany(part => ReadPart(part.Path)).ToArray();
        Assert.Equal(9, report.Written.Messages);
        Assert.Contains(exported, item => item.Folder == "Hidden Root" && item.Document.Subject == "Message hidden-root-1");
        Assert.Contains(exported, item => item.Folder == "Inbox\\Hidden Child" && item.Document.Subject == "Message hidden-child-1");
    }

    [Theory]
    [InlineData("expired-numbered", false)]
    [InlineData("expired-large-numbered", false)]
    [InlineData("incomplete-numbered", true)]
    [InlineData("spring-forward-first-day", true)]
    [InlineData("recurrence-zone-overlap", true)]
    [InlineData("recurrence-zone-ended", false)]
    [InlineData("unbounded-series", true)]
    public async Task CalendarSeriesDateFilterUsesFiniteOccurrencesAndTheirRecurrenceZone(string scenario, bool included) {
        var options = NewOptions("calendar-" + scenario + ".pst", GraphMailboxExportItemTypes.Calendar);
        options.StartDate = new DateTimeOffset(2026, 1, 11, 8, 30, 0, TimeSpan.Zero);
        if (scenario == "expired-large-numbered") {
            _graph.EventsJson = RecurringCalendarPage("2020-01-06T09:00:00", "2020-01-06T09:30:00", "UTC",
                "{\"type\":\"daily\",\"interval\":1}",
                "{\"type\":\"numbered\",\"startDate\":\"2020-01-06\",\"numberOfOccurrences\":1001,\"recurrenceTimeZone\":\"UTC\"}");
        } else if (scenario == "incomplete-numbered") {
            _graph.EventsJson = RecurringCalendarPage("2020-01-06T09:00:00", "2020-01-06T09:30:00", "UTC",
                "{\"type\":\"relativeMonthly\",\"interval\":1,\"index\":\"first\"}",
                "{\"type\":\"numbered\",\"startDate\":\"2020-01-06\",\"numberOfOccurrences\":10,\"recurrenceTimeZone\":\"UTC\"}");
        } else if (scenario == "spring-forward-first-day") {
            options.StartDate = new DateTimeOffset(2026, 3, 16, 6, 30, 0, TimeSpan.Zero);
            _graph.EventsJson = RecurringCalendarPage("2026-03-08T00:00:00", "2026-03-09T00:00:00", "Pacific Standard Time",
                "{\"type\":\"weekly\",\"interval\":1,\"daysOfWeek\":[\"sunday\"]}",
                "{\"type\":\"numbered\",\"startDate\":\"2026-03-08\",\"numberOfOccurrences\":2,\"recurrenceTimeZone\":\"Pacific Standard Time\"}");
        } else if (scenario is "expired-numbered" or "unbounded-series") {
            string range = scenario == "expired-numbered"
                ? "{\"type\":\"numbered\",\"startDate\":\"2020-01-06\",\"numberOfOccurrences\":10,\"recurrenceTimeZone\":\"UTC\"}"
                : "{\"type\":\"noEnd\",\"startDate\":\"2020-01-06\",\"recurrenceTimeZone\":\"UTC\"}";
            _graph.EventsJson = RecurringCalendarPage("2020-01-06T09:00:00", "2020-01-06T09:30:00", "UTC",
                "{\"type\":\"weekly\",\"interval\":1,\"daysOfWeek\":[\"monday\"],\"firstDayOfWeek\":\"monday\"}", range);
        } else {
            // The final 20:00–01:00 Pacific occurrence overlaps 08:30Z the following UTC day.
            bool overlap = scenario == "recurrence-zone-overlap";
            string start = overlap ? "2026-01-10" : "2026-01-08";
            string end = overlap ? "2026-01-11" : "2026-01-09";
            string until = overlap ? "2026-01-10" : "2026-01-09";
            _graph.EventsJson = RecurringCalendarPage(start + "T20:00:00", end + "T01:00:00", "Pacific Standard Time",
                "{\"type\":\"daily\",\"interval\":1}",
                "{\"type\":\"endDate\",\"startDate\":\"" + start + "\",\"endDate\":\"" + until + "\",\"recurrenceTimeZone\":\"Pacific Standard Time\"}");
        }

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.Equal(included ? 1 : 0, report.Written.Events);
        var exported = report.Parts.SelectMany(part => ReadPart(part.Path)).ToArray();
        Assert.Equal(included ? 1 : 0, exported.Length);
        if (included) {
            EmailDocument calendar = Assert.Single(exported).Document;
            Assert.Equal("Filtered series", calendar.Subject);
            Assert.Equal(OutlookItemKind.Appointment, calendar.OutlookItemKind);
            if (scenario == "incomplete-numbered") {
                Assert.Null(calendar.Appointment!.Recurrence);
                Assert.Contains(report.Issues, issue => issue.Kind == GraphMailboxExportIssueKind.Partial && issue.Message.Contains("missing its weekdays"));
            } else {
                Assert.NotNull(calendar.Appointment!.Recurrence);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResumeRejectsMissingOrModifiedCompletedPartBeforeWritingMoreOutput(bool deletePart) {
        var options = NewOptions("integrity.pst", GraphMailboxExportItemTypes.Mail);
        options.Split = new EmailStorePstSplitOptions(maxEstimatedBytesPerPart: 40 * 1024);
        options.CheckpointIntervalItems = 2;
        await InterruptExportAsync(options, "projects-1");
        string completed = GraphMailboxPstExporter.GetPartPath(options.DestinationPath, 1);
        Assert.Equal(2, ReadPart(completed).Count);
        if (deletePart) File.Delete(completed);
        else using (FileStream file = new FileStream(completed, FileMode.Append, FileAccess.Write)) file.WriteByte(0x7f);
        string[] before = SnapshotPstFiles();
        int requests = _graph.MimeRequests.Count;
        options.Resume = true;

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => NewExporter(options).ExportAsync());

        Assert.Contains("Completed PST part", error.Message);
        Assert.Contains(deletePart ? "is missing" : "checkpoint SHA-256", error.Message);
        Assert.Equal(requests, _graph.MimeRequests.Count);
        Assert.Equal(before, SnapshotPstFiles());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResumeRejectsChangedFolderArraysWhoseSemicolonJoinedTextIsIdentical(bool exclude) {
        var options = NewOptions("folder-fingerprint.pst", GraphMailboxExportItemTypes.Mail);
        IList<string> patterns = exclude ? options.ExcludeFolders : options.IncludeFolders;
        if (!exclude) _graph.InboxDisplayName = "Inbox;Archive";
        patterns.Add("Inbox;Archive");
        options.CheckpointIntervalItems = 2;
        await InterruptExportAsync(options, "inbox-3");
        patterns.Clear();
        patterns.Add("Inbox");
        patterns.Add("Archive");
        int requests = _graph.MimeRequests.Count;
        options.Resume = true;

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => NewExporter(options).ExportAsync());

        Assert.Contains("folder filters", error.Message);
        Assert.Equal(requests, _graph.MimeRequests.Count);
    }

    private async Task InterruptExportAsync(GraphMailboxPstExportOptions options, string stopBeforeMessage) {
        using (var cancellation = new CancellationTokenSource()) {
            _graph.OnMimeRequest = (id, _) => { if (id == stopBeforeMessage) cancellation.Cancel(); };
            try {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewExporter(options).ExportAsync(cancellation.Token));
            } finally {
                _graph.OnMimeRequest = null;
            }
        }
        Assert.True(File.Exists(options.CheckpointPath));
    }

    private string[] SnapshotPstFiles() => Directory.GetFiles(_directory, "*.pst", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => path + ":" + Sha256(path)).ToArray();

    private static string RecurringCalendarPage(string start, string end, string zone, string pattern, string range) =>
        "{\"value\":[{\"id\":\"event-filtered\",\"subject\":\"Filtered series\",\"type\":\"seriesMaster\"," +
        "\"start\":{\"dateTime\":\"" + start + "\",\"timeZone\":\"" + zone + "\"}," +
        "\"end\":{\"dateTime\":\"" + end + "\",\"timeZone\":\"" + zone + "\"},\"originalStartTimeZone\":\"" + zone + "\"," +
        "\"recurrence\":{\"pattern\":" + pattern + ",\"range\":" + range + "}}]}";

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
        internal ConcurrentQueue<string> FolderListRequests { get; } = new ConcurrentQueue<string>();
        internal Action<string, int>? OnMimeRequest { get; set; }
        internal bool IncludeHiddenFixture { get; set; }
        internal string InboxDisplayName { get; set; } = "Inbox";
        internal string? EventsJson { get; set; }

        internal Dictionary<string, (string Id, DateTimeOffset Received)[]> Messages { get; } =
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
                },
                ["hidden-root-id"] = new[] {
                    ("hidden-root-1", new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero))
                },
                ["hidden-child-id"] = new[] {
                    ("hidden-child-1", new DateTimeOffset(2026, 1, 13, 8, 0, 0, TimeSpan.Zero))
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
                    FolderListRequests.Enqueue(Uri.UnescapeDataString(uri));
                    return Json("{\"value\":[" +
                        Folder("inbox-id", InboxDisplayName, Messages["inbox-id"].Length, 1) + "," +
                        Folder("sent-id", "Sent Items", 1, 0) + "," +
                        Folder("deleted-id", "Deleted Items", 1, 0) +
                        (IncludeHiddenFixture && IncludesHidden(query) ? "," + Folder("hidden-root-id", "Hidden Root", 1, 0) : string.Empty) + "]}");
                case "mailFolders/inbox-id/childFolders":
                    FolderListRequests.Enqueue(Uri.UnescapeDataString(uri));
                    return Json("{\"value\":[" + Folder("projects-id", "Projects", 2, 0) +
                        (IncludeHiddenFixture && IncludesHidden(query) ? "," + Folder("hidden-child-id", "Hidden Child", 1, 0) : string.Empty) + "]}");
                case "contactFolders":
                    return Json("{\"value\":[]}");
                case "contacts":
                    return Json("{\"value\":[{\"id\":\"contact-1\",\"displayName\":\"Ada Lovelace\",\"givenName\":\"Ada\",\"surname\":\"Lovelace\"," +
                        "\"emailAddresses\":[{\"name\":\"Ada Lovelace\",\"address\":\"ada@contoso.com\"}],\"businessPhones\":[\"+1 555 0100\"]," +
                        "\"companyName\":\"Analytical Engines\"}]}");
                case "calendars":
                    return Json("{\"value\":[{\"id\":\"calendar-id\",\"name\":\"Calendar\",\"isDefaultCalendar\":true}]}");
                case "calendars/calendar-id/events":
                    if (EventsJson != null) return Json(EventsJson);
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
            Match from = Regex.Match(query, @"receivedDateTime ge ([^\s&]+)");
            if (from.Success) {
                DateTimeOffset bound = DateTimeOffset.Parse(from.Groups[1].Value, CultureInfo.InvariantCulture);
                messages = messages.Where(message => message.Received >= bound);
            }
            Match before = Regex.Match(query, @"receivedDateTime lt ([^\s&]+)");
            if (before.Success) {
                DateTimeOffset bound = DateTimeOffset.Parse(before.Groups[1].Value, CultureInfo.InvariantCulture);
                messages = messages.Where(message => message.Received < bound);
            }
            // Two messages per page so paging continues through @odata.nextLink.
            int skip = query.Contains("$skip=2") ? 2 : 0;
            var page = messages.Skip(skip).Take(2).ToArray();
            string? next = skip == 0 && messages.Count() > 2 ? uri + "&$skip=2" : null;
            var body = new StringBuilder("{\"value\":[");
            body.Append(string.Join(",", page.Select(message =>
                "{\"id\":\"" + message.Id + "\",\"subject\":\"Message " + message.Id + "\",\"receivedDateTime\":\"" +
                message.Received.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture) + "\",\"isRead\":true,\"isDraft\":false," +
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

        private static bool IncludesHidden(string query) => query.IndexOf("includeHiddenFolders=true", StringComparison.OrdinalIgnoreCase) >= 0;

        private static HttpResponseMessage Json(string body) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
