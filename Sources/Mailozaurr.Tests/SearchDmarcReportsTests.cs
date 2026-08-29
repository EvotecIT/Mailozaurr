using MailKit;
using MailKit.Net.Pop3;
using Mailozaurr;
using Mailozaurr.DmarcReports;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class SearchDmarcReportsTests {
    private static MimeMessage CreateDmarc(string domain, DateTimeOffset date) {
        var message = new MimeMessage();
        message.Subject = $"Report domain: {domain}";
        message.Date = date;
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var builder = new BodyBuilder();
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
            var entry = zip.CreateEntry("report.xml");
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(
                $"<feedback><policy_published><domain>{domain}</domain></policy_published></feedback>");
            entryStream.Write(bytes, 0, bytes.Length);
        }
        ms.Position = 0;
        var part = new MimePart("application", "zip") {
            Content = new MimeContent(ms),
            FileName = $"{domain}.zip"
        };
        builder.Attachments.Add(part);
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static MimeMessage CreateXmlDmarc(string domain, DateTimeOffset date, string mediaType, string mediaSubtype, string fileName) {
        var message = new MimeMessage();
        message.Subject = $"Report domain: {domain}";
        message.Date = date;
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var builder = new BodyBuilder();
        var xml = $"<feedback><policy_published><domain>{domain}</domain></policy_published></feedback>";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var part = new MimePart(mediaType, mediaSubtype) {
            Content = new MimeContent(ms),
            FileName = fileName
        };
        builder.Attachments.Add(part);
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static MimeMessage CreateZippedXmlDmarc(string domain, DateTimeOffset date) {
        var message = new MimeMessage();
        message.Subject = "Report";
        message.Date = date;
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var builder = new BodyBuilder();
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
            var entry = zip.CreateEntry("report.xml");
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes($"<feedback><policy_published><domain>{domain}</domain></policy_published></feedback>");
            entryStream.Write(bytes, 0, bytes.Length);
        }
        ms.Position = 0;
        var part = new MimePart("application", "zip") {
            Content = new MimeContent(ms),
            FileName = "report.zip"
        };
        builder.Attachments.Add(part);
        message.Body = builder.ToMessageBody();
        return message;
    }

    private class TrackingPop3Client : Pop3Client {
        private readonly List<MimeMessage> _messages;
        private readonly object _fetchGate = new();
        public List<int> FetchedIndexes { get; } = new();
        public int FetchCount {
            get {
                lock (_fetchGate) return FetchedIndexes.Count;
            }
        }
        public TrackingPop3Client(IEnumerable<MimeMessage> messages) {
            _messages = new List<MimeMessage>(messages);
        }
        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => _messages.Count;
        public override async Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            await Task.Yield();
            lock (_fetchGate) FetchedIndexes.Add(index);
            return _messages[index];
        }
    }

    private sealed class CancelablePop3Client : Pop3Client {
        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => 4;
        public override async Task<MimeMessage> GetMessageAsync(
            int index,
            CancellationToken cancellationToken = default,
            ITransferProgress? progress = null) {
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class BlockingReadStream : MemoryStream {
        private readonly ManualResetEventSlim _release = new(false);
        internal TaskCompletionSource<object?> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal BlockingReadStream(byte[] content) : base(content, writable: false) { }

        internal void Release() => _release.Set();

        public override int Read(byte[] buffer, int offset, int count) {
            Started.TrySetResult(null);
            _release.Wait();
            return base.Read(buffer, offset, count);
        }
    }

    [Fact]
    public async Task SharedReadBudget_SerializesReservationsAndRefundsUnusedBytes() {
        var operation = new SharedReadBudget(2);
        var firstSource = new BlockingReadStream(new byte[] { 1 });
        using var first = new SharedBudgetReadStream(
            firstSource,
            new SharedReadBudget(2),
            operation);
        using var second = new SharedBudgetReadStream(
            new MemoryStream(new byte[] { 2 }),
            new SharedReadBudget(2),
            operation);
        var firstBuffer = new byte[2];
        var secondBuffer = new byte[1];

        Task<int> firstRead = Task.Run(() => first.Read(firstBuffer, 0, firstBuffer.Length));
        await firstSource.Started.Task;
        Task<int> secondRead = Task.Run(() => second.Read(secondBuffer, 0, secondBuffer.Length));
        Assert.False(secondRead.IsCompleted);

        firstSource.Release();
        Assert.Equal(1, await firstRead);
        Assert.Equal(1, await secondRead);
        Assert.Equal((byte)2, secondBuffer[0]);
    }

    [Fact]
    public async Task SearchDmarcReportsAsync_PropagatesCallerCancellationFromParallelPop3Workers() {
        using var client = new CancelablePop3Client();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MailboxSearcher.SearchDmarcReportsAsync(
                client,
                new DmarcReportInspectionOptions(),
                parallelDownloadLimit: 2,
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task SearchDmarcReportsAsync_StopsAtConfiguredMessageScanLimit() {
        var now = DateTimeOffset.UtcNow;
        using var client = new TrackingPop3Client(new[] {
            CreateDmarc("one.example", now),
            CreateDmarc("two.example", now),
            CreateDmarc("three.example", now)
        });
        var options = new DmarcReportInspectionOptions { MaxMessagesScanned = 1 };

        IList<DmarcReport> reports = await MailboxSearcher.SearchDmarcReportsAsync(
            client,
            options,
            parallelDownloadLimit: 1);

        Assert.Single(reports);
        Assert.Equal(1, client.FetchCount);
        Assert.Equal(new[] { 2 }, client.FetchedIndexes);
    }

    [Fact]
    public async Task SearchDmarcReportsAsync_ParallelScanUsesNewestPop3Messages() {
        var now = DateTimeOffset.UtcNow;
        using var client = new TrackingPop3Client(new[] {
            CreateDmarc("one.example", now),
            CreateDmarc("two.example", now),
            CreateDmarc("three.example", now),
            CreateDmarc("four.example", now),
            CreateDmarc("five.example", now)
        });
        var options = new DmarcReportInspectionOptions { MaxMessagesScanned = 2 };

        IList<DmarcReport> reports = await MailboxSearcher.SearchDmarcReportsAsync(
            client,
            options,
            parallelDownloadLimit: 2);

        Assert.Equal(2, reports.Count);
        Assert.Equal(new[] { 3, 4 }, client.FetchedIndexes.OrderBy(index => index));
    }

    [Fact]
    public void DmarcMimeDownloadBudget_RejectsPerMessageAndAggregateOverruns() {
        var budget = new DmarcMimeDownloadBudget(maxBytesPerMessage: 5, maxTotalBytes: 7);
        ITransferProgress first = budget.CreateTransferProgress();
        first.Report(5, 5);
        ITransferProgress second = budget.CreateTransferProgress();

        Assert.Throws<InvalidDataException>(() => second.Report(3, 3));
        Assert.Throws<InvalidDataException>(() =>
            budget.CreateTransferProgress().Report(6, 6));
    }

    [Fact]
    public async Task DmarcMimeDownloadBudget_RetainsReservationWhenProviderParsingFails() {
        var budget = new DmarcMimeDownloadBudget(maxBytesPerMessage: 5, maxTotalBytes: 7);

        await Assert.ThrowsAsync<FormatException>(() => budget.DownloadAsync(
            (_, _) => throw new FormatException("Malformed MIME payload."),
            CancellationToken.None));

        long secondLimit = 0;
        MimeMessage second = await budget.DownloadAsync(
            (limit, _) => {
                secondLimit = limit;
                return Task.FromResult(new BoundedMimeMessage(new MimeMessage(), limit));
            },
            CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(2, secondLimit);
        await Assert.ThrowsAsync<InvalidDataException>(() => budget.DownloadAsync(
            (_, _) => Task.FromResult(new BoundedMimeMessage(new MimeMessage(), 0)),
            CancellationToken.None));
    }

    [Fact]
    public async Task DmarcMimeDownloadBudget_RefundsUnusedReservationAfterSuccess() {
        var budget = new DmarcMimeDownloadBudget(maxBytesPerMessage: 5, maxTotalBytes: 7);

        await budget.DownloadAsync(
            (_, _) => Task.FromResult(new BoundedMimeMessage(new MimeMessage(), 2)),
            CancellationToken.None);

        long secondLimit = 0;
        await budget.DownloadAsync(
            (limit, _) => {
                secondLimit = limit;
                return Task.FromResult(new BoundedMimeMessage(new MimeMessage(), limit));
            },
            CancellationToken.None);

        Assert.Equal(5, secondLimit);
    }

    [Fact]
    public async Task DmarcMimeDownloadBudget_AllowsReservedDownloadsToRunConcurrently() {
        var budget = new DmarcMimeDownloadBudget(maxBytesPerMessage: 5, maxTotalBytes: 10);
        var firstStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<MimeMessage> first = budget.DownloadAsync(
            async (limit, _) => {
                firstStarted.TrySetResult(null);
                await release.Task;
                return new BoundedMimeMessage(new MimeMessage(), limit);
            },
            CancellationToken.None);
        Task<MimeMessage> second = budget.DownloadAsync(
            async (limit, _) => {
                secondStarted.TrySetResult(null);
                await release.Task;
                return new BoundedMimeMessage(new MimeMessage(), limit);
            },
            CancellationToken.None);

        Task bothStarted = Task.WhenAll(firstStarted.Task, secondStarted.Task);
        Assert.Same(bothStarted, await Task.WhenAny(bothStarted, Task.Delay(TimeSpan.FromSeconds(2))));
        release.TrySetResult(null);
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task DmarcMimeDownloadBudget_WaitsForInFlightRefundBeforeRejecting() {
        var budget = new DmarcMimeDownloadBudget(maxBytesPerMessage: 5, maxTotalBytes: 5);
        var firstStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        long secondLimit = 0;

        Task<MimeMessage> first = budget.DownloadAsync(
            async (limit, _) => {
                firstStarted.TrySetResult(null);
                await releaseFirst.Task;
                return new BoundedMimeMessage(new MimeMessage(), 1);
            },
            CancellationToken.None);
        await firstStarted.Task;
        Task<MimeMessage> second = budget.DownloadAsync(
            (limit, _) => {
                secondLimit = limit;
                return Task.FromResult(new BoundedMimeMessage(new MimeMessage(), limit));
            },
            CancellationToken.None);

        Assert.False(second.IsCompleted);
        releaseFirst.TrySetResult(null);
        await Task.WhenAll(first, second);

        Assert.Equal(4, secondLimit);
    }

    [Fact]
    public void FilterDmarcReports_ExtractsAttachments() {
        var now = DateTimeOffset.UtcNow;
        var msg = CreateDmarc("example.com", now);
        var list = new List<MimeMessage> { msg };
        var reports = MailboxSearcher.FilterDmarcReports(list, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com");
        Assert.Single(reports);
        var report = reports[0];
        Assert.Equal("reporter@example.com", report.From);
        Assert.Single(report.Attachments);
        Assert.EndsWith(".zip", report.Attachments[0].Name);
        using var ms = new MemoryStream();
        report.Attachments[0].Content.CopyTo(ms);
        Assert.True(ms.Length > 0);
    }

    [Fact]
    public void FilterDmarcReports_MatchesDomainInAttachmentName() {
        var now = DateTimeOffset.UtcNow;
        var msg = CreateDmarc("example.com", now);
        msg.Subject = "Report";
        var list = new List<MimeMessage> { msg };
        var reports = MailboxSearcher.FilterDmarcReports(list, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com");
        Assert.Single(reports);
        Assert.Single(reports[0].Attachments);
    }

    [Fact]
    public void FilterDmarcReports_MatchesDomainInXmlContent() {
        var now = DateTimeOffset.UtcNow;
        var msg = CreateZippedXmlDmarc("example.com", now);
        var list = new List<MimeMessage> { msg };
        var reports = MailboxSearcher.FilterDmarcReports(list, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com");
        Assert.Single(reports);
    }

    [Fact]
    public void FilterDmarcReports_FiltersOutMismatchedDomain() {
        var now = DateTimeOffset.UtcNow;
        var good = CreateDmarc("example.com", now);
        good.Subject = "Report";
        var bad = CreateDmarc("other.com", now);
        bad.Subject = "Report";
        var list = new List<MimeMessage> { good, bad };
        var reports = MailboxSearcher.FilterDmarcReports(list, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com");
        Assert.Single(reports);
        Assert.All(reports[0].Attachments, a => Assert.Contains("example.com", a.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildDmarcReportSearchQuery_ContainsSubject() {
        var query = MailboxSearcher.BuildDmarcReportSearchQuery(null, null, "example.com");
        bool Contains(MailKit.Search.SearchQuery q) {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var term = q.GetType().GetProperty("Term", flags)?.GetValue(q)?.ToString();
            if (term == "SubjectContains") {
                var text = q.GetType().GetProperty("Text", flags)?.GetValue(q)?.ToString();
                if (text?.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0 == true) return true;
            }
            var left = q.GetType().GetProperty("Left", flags)?.GetValue(q) as MailKit.Search.SearchQuery;
            var right = q.GetType().GetProperty("Right", flags)?.GetValue(q) as MailKit.Search.SearchQuery;
            if (left != null && Contains(left)) return true;
            if (right != null && Contains(right)) return true;
            return false;
        }
        Assert.True(Contains(query));
    }

    [Fact]
    public void BuildDmarcReportSearchQuery_RequiresAttachments() {
        var query = MailboxSearcher.BuildDmarcReportSearchQuery(null, null, null);
        bool ContainsAttachment(MailKit.Search.SearchQuery q) {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var term = q.GetType().GetProperty("Term", flags)?.GetValue(q)?.ToString();
            if (term == "HasAttachment") return true;
            if (term == "HeaderContains") {
                var field = q.GetType().GetProperty("Field", flags)?.GetValue(q)?.ToString();
                var value = q.GetType().GetProperty("Value", flags)?.GetValue(q)?.ToString();
                if (field?.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) == true &&
                    value?.IndexOf("attachment", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            var left = q.GetType().GetProperty("Left", flags)?.GetValue(q) as MailKit.Search.SearchQuery;
            var right = q.GetType().GetProperty("Right", flags)?.GetValue(q) as MailKit.Search.SearchQuery;
            if (left != null && ContainsAttachment(left)) return true;
            if (right != null && ContainsAttachment(right)) return true;
            return false;
        }
        Assert.True(ContainsAttachment(query));
    }

    [Fact]
    public void BuildGmailDmarcReportQuery_IncludesDomainAndDates() {
        var since = new DateTime(2024, 1, 1);
        var before = new DateTime(2024, 2, 1);
        var q = MailboxSearcher.BuildGmailDmarcReportQuery(since, before, "example.com");
        Assert.Contains("example.com", q);
        Assert.Contains("after:2024/01/01", q);
        Assert.Contains("before:2024/02/01", q);
        Assert.Contains("has:attachment", q);
    }

    [Fact]
    public void BuildGmailDmarcReportQuery_DomainWithDots_ReturnsQuery() {
        var domain = "sub.example.co.uk";
        var q = MailboxSearcher.BuildGmailDmarcReportQuery(null, null, domain);
        Assert.Contains(domain, q);
    }

    [Fact]
    public void BuildGmailDmarcReportQuery_DomainWithQuotes_EscapesQuotes() {
        var domain = "exa\"mple.com";
        var q = MailboxSearcher.BuildGmailDmarcReportQuery(null, null, domain);
        Assert.Contains("exa\\\"mple.com", q);
    }

    [Fact]
    public void FilterDmarcReports_FiltersAcrossTimeZones() {
        var msg1 = CreateDmarc("example.com", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)));
        var msg2 = CreateDmarc("example.com", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5)));
        var list = new List<MimeMessage> { msg1, msg2 };
        var since = new DateTime(2023, 12, 31, 21, 0, 0, DateTimeKind.Utc);
        var before = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var reports = MailboxSearcher.FilterDmarcReports(list, since, before, domain: null);
        Assert.Single(reports);
        Assert.Equal(msg1.Subject, reports[0].Subject);
    }

    [Fact]
    public void FilterDmarcReports_ExtractsXmlAttachmentByExtension() {
        var now = DateTimeOffset.UtcNow;
        var msg = CreateXmlDmarc("example.com", now, "application", "octet-stream", "example.xml");
        var list = new List<MimeMessage> { msg };
        var reports = MailboxSearcher.FilterDmarcReports(list, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com");
        Assert.Single(reports);
        Assert.Single(reports[0].Attachments);
        Assert.EndsWith(".xml", reports[0].Attachments[0].Name);
    }

    [Theory]
    [InlineData("application")]
    [InlineData("text")]
    public void FilterDmarcReports_ExtractsXmlAttachmentByContentType(string mediaType) {
        var now = DateTimeOffset.UtcNow;
        var msg = CreateXmlDmarc("example.com", now, mediaType, "xml", "example");
        var list = new List<MimeMessage> { msg };
        var reports = MailboxSearcher.FilterDmarcReports(list, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com");
        Assert.Single(reports);
        Assert.Single(reports[0].Attachments);
        Assert.Equal("example", reports[0].Attachments[0].Name);
    }

    [Fact]
    public async Task SearchDmarcReportsAsync_Pop3_StopsAfterMaxResults() {
        var now = DateTimeOffset.UtcNow;
        var msgs = new List<MimeMessage>();
        msgs.Add(new MimeMessage());
        msgs.Add(CreateDmarc("example.com", now));
        msgs.Add(CreateDmarc("example.com", now));
        msgs.Add(CreateDmarc("example.com", now));
        var client = new TrackingPop3Client(msgs);
        var reports = await MailboxSearcher.SearchDmarcReportsAsync(
            client,
            maxResults: 2,
            parallelDownloadLimit: 2,
            cancellationToken: CancellationToken.None);
        Assert.Equal(2, reports.Count);
        Assert.True(client.FetchCount <= 4, $"fetched {client.FetchCount}");
    }

    [Fact]
    public async Task SearchDmarcReportsAsync_Graph_PropagatesCancellationToMimeDownload() {
        var handler = new CancelDuringGraphMimeHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
            using var cts = new CancellationTokenSource();
            var searchTask = GraphMailboxSearcher.SearchDmarcReportsAsync(
                cred,
                "user/name@example.com",
                cancellationToken: cts.Token);

            var startedTask = handler.MimeStarted.Task;
            var completed = await Task.WhenAny(startedTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(startedTask, completed);

            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await searchTask);

            Assert.True(handler.MimeRequestCanceled);
            Assert.Contains("user%2Fname%40example.com", handler.MimeRequestUri!.AbsoluteUri, StringComparison.Ordinal);
            Assert.Contains("A%2FB%23C", handler.MimeRequestUri.AbsoluteUri, StringComparison.Ordinal);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public void FilterDmarcReports_IgnoresMalformedArchive() {
        var now = DateTimeOffset.UtcNow;
        var message = new MimeMessage();
        message.Subject = "Report";
        message.Date = now;
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var builder = new BodyBuilder();
        var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var part = new MimePart("application", "zip") {
            Content = new MimeContent(ms),
            FileName = "report.zip"
        };
        builder.Attachments.Add(part);
        message.Body = builder.ToMessageBody();
        bool logged = false;
        void Handler(object? s, LogEventArgs e) => logged = true;
        LoggingMessages.Logger.OnErrorMessage += Handler;
        try {
            var reports = MailboxSearcher.FilterDmarcReports(new[] { message }, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com", maxUncompressedSize: 1024);
            Assert.Empty(reports);
            Assert.True(logged);
        } finally {
            LoggingMessages.Logger.OnErrorMessage -= Handler;
        }
    }

    [Fact]
    public void FilterDmarcReports_SkipsOversizedAttachment() {
        var now = DateTimeOffset.UtcNow;
        var message = new MimeMessage();
        message.Subject = "Report";
        message.Date = now;
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var builder = new BodyBuilder();
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
            var entry = zip.CreateEntry("report.xml");
            using var entryStream = entry.Open();
            var large = new string('a', 2048);
            var bytes = Encoding.UTF8.GetBytes($"<feedback><policy_published><domain>example.com</domain></policy_published><data>{large}</data></feedback>");
            entryStream.Write(bytes, 0, bytes.Length);
        }
        ms.Position = 0;
        var part = new MimePart("application", "zip") {
            Content = new MimeContent(ms),
            FileName = "report.zip"
        };
        builder.Attachments.Add(part);
        message.Body = builder.ToMessageBody();
        bool logged = false;
        void Handler(object? s, LogEventArgs e) => logged = true;
        LoggingMessages.Logger.OnErrorMessage += Handler;
        try {
            var reports = MailboxSearcher.FilterDmarcReports(new[] { message }, since: now.AddMinutes(-1).DateTime, before: now.AddMinutes(1).DateTime, domain: "example.com", maxUncompressedSize: 512);
            Assert.Empty(reports);
            Assert.True(logged);
        } finally {
            LoggingMessages.Logger.OnErrorMessage -= Handler;
        }
    }

    [Fact]
    public void FilterDmarcReports_RejectsAggregateZipExpansionBeyondAttachmentLimit() {
        var now = DateTimeOffset.UtcNow;
        var message = CreateZipMessage(
            "example.com.zip",
            now,
            ("first.xml", CreateXmlPayload("example.com", 300)),
            ("second.xml", CreateXmlPayload("example.com", 300)));

        var reports = MailboxSearcher.FilterDmarcReports(
            new[] { message },
            since: null,
            before: null,
            domain: "example.com",
            maxUncompressedSize: 512);

        Assert.Empty(reports);
    }

    [Fact]
    public void FilterDmarcReports_RejectsZipWithTooManyEntries() {
        var now = DateTimeOffset.UtcNow;
        var message = CreateZipMessage(
            "example.com.zip",
            now,
            ("first.xml", CreateXmlPayload("example.com", 0)),
            ("second.xml", CreateXmlPayload("example.com", 0)));
        var options = new DmarcReportInspectionOptions {
            MaxArchiveEntriesPerAttachment = 1
        };
        var policy = options.CreatePolicy();

        var reports = MailboxSearcher.FilterDmarcReports(
            new[] { message },
            since: null,
            before: null,
            domain: "example.com",
            policy,
            new SharedReadBudget(policy.MaxTotalUncompressedBytes));

        Assert.Empty(reports);
    }

    [Fact]
    public void FilterDmarcReports_SharesExpandedByteBudgetAcrossMessages() {
        var now = DateTimeOffset.UtcNow;
        var first = CreateXmlDmarc("example.com", now, "application", "xml", "example.com.xml");
        var second = CreateXmlDmarc("example.com", now, "application", "xml", "example.com.xml");
        var options = new DmarcReportInspectionOptions {
            MaxUncompressedBytesPerAttachment = 100,
            MaxTotalUncompressedBytes = 130
        };
        var policy = options.CreatePolicy();

        var reports = MailboxSearcher.FilterDmarcReports(
            new[] { first, second },
            since: null,
            before: null,
            domain: "example.com",
            policy,
            new SharedReadBudget(policy.MaxTotalUncompressedBytes));

        Assert.Single(reports);
    }

    [Fact]
    public void FilterDmarcReports_RejectsGzipExpansionBeyondAttachmentLimit() {
        var now = DateTimeOffset.UtcNow;
        var message = new MimeMessage {
            Subject = "Report",
            Date = now
        };
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, true)) {
            byte[] payload = Encoding.UTF8.GetBytes(CreateXmlPayload("example.com", 2048));
            gzip.Write(payload, 0, payload.Length);
        }
        compressed.Position = 0;
        var builder = new BodyBuilder();
        builder.Attachments.Add(new MimePart("application", "gzip") {
            Content = new MimeContent(compressed),
            FileName = "example.com.xml.gz"
        });
        message.Body = builder.ToMessageBody();

        var reports = MailboxSearcher.FilterDmarcReports(
            new[] { message },
            since: null,
            before: null,
            domain: "example.com",
            maxUncompressedSize: 512);

        Assert.Empty(reports);
    }

    [Fact]
    public void FilterDmarcReports_PropagatesCancellationDuringAttachmentInspection() {
        var now = DateTimeOffset.UtcNow;
        var message = CreateXmlDmarc("example.com", now, "application", "xml", "example.com.xml");
        var options = new DmarcReportInspectionOptions();
        var policy = options.CreatePolicy();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => MailboxSearcher.FilterDmarcReports(
            new[] { message },
            since: null,
            before: null,
            domain: "example.com",
            policy,
            new SharedReadBudget(policy.MaxTotalUncompressedBytes),
            cancellation.Token));
    }

    private static MimeMessage CreateZipMessage(
        string fileName,
        DateTimeOffset date,
        params (string Name, string Content)[] entries) {
        var message = new MimeMessage {
            Subject = "Report",
            Date = date
        };
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true)) {
            foreach (var item in entries) {
                var entry = zip.CreateEntry(item.Name);
                using var entryStream = entry.Open();
                byte[] bytes = Encoding.UTF8.GetBytes(item.Content);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }
        archive.Position = 0;
        var builder = new BodyBuilder();
        builder.Attachments.Add(new MimePart("application", "zip") {
            Content = new MimeContent(archive),
            FileName = fileName
        });
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static string CreateXmlPayload(string domain, int paddingLength) =>
        $"<feedback><policy_published><domain>{domain}</domain></policy_published><data>{new string('x', paddingLength)}</data></feedback>";

    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    private sealed class CancelDuringGraphMimeHandler : HttpMessageHandler {
        public TaskCompletionSource<object?> MimeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool MimeRequestCanceled { get; private set; }
        public Uri? MimeRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var uri = request.RequestUri!;
            if (uri.AbsoluteUri.Contains("oauth2")) {
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }

            if (uri.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal)) {
                var json = "{\"value\":[{\"id\":\"A/B#C\"}]}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }

            if (uri.AbsolutePath.IndexOf("/messages/", StringComparison.Ordinal) >= 0 && uri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal)) {
                MimeRequestUri = uri;
                MimeStarted.TrySetResult(null);
                try {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    MimeRequestCanceled = true;
                    throw;
                }

                const string raw = "Date: Mon, 1 Jan 2024 00:00:00 +0000\r\nSubject: report domain: example.com\r\n\r\nbody";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(raw) };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
