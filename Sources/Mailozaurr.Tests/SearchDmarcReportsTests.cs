using Mailozaurr;
using Mailozaurr.DmarcReports;
using MailKit;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Pop3;
using Xunit;

namespace Mailozaurr.Tests;

public class SearchDmarcReportsTests {
    private static MimeMessage CreateDmarc(string domain, DateTimeOffset date) {
        var message = new MimeMessage();
        message.Subject = $"Report domain: {domain}";
        message.Date = date;
        message.From.Add(new MailboxAddress("reporter", "reporter@example.com"));
        var builder = new BodyBuilder();
        var ms = new MemoryStream(Encoding.UTF8.GetBytes("dummy"));
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
        public int FetchCount { get; private set; }
        public TrackingPop3Client(IEnumerable<MimeMessage> messages) {
            _messages = new List<MimeMessage>(messages);
        }
        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => _messages.Count;
        public override async Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            await Task.Yield();
            FetchCount++;
            return _messages[index];
        }
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
}
