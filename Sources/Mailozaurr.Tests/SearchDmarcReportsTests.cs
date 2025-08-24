using Mailozaurr;
using Mailozaurr.DmarcReports;
using MimeKit;
using MailKit;
using MailKit.Net.Pop3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        var ms = new MemoryStream(Encoding.UTF8.GetBytes("<feedback/>"));
        var part = new MimePart(mediaType, mediaSubtype) {
            Content = new MimeContent(ms),
            FileName = fileName
        };
        builder.Attachments.Add(part);
        message.Body = builder.ToMessageBody();
        return message;
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
        Assert.True(report.Attachments[0].Content.Length > 0);
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

    private class CountingPop3Client : Pop3Client {
        private readonly List<MimeMessage> _messages;
        private readonly int _delay;
        private readonly object _lock = new();
        private int _current;
        public int MaxConcurrency { get; private set; }
        public int CallCount { get; private set; }
        public CountingPop3Client(IEnumerable<MimeMessage> messages, int delay) {
            _messages = new List<MimeMessage>(messages);
            _delay = delay;
        }
        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => _messages.Count;
        public override async Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            lock (_lock) {
                _current++;
                CallCount++;
                if (_current > MaxConcurrency) MaxConcurrency = _current;
            }
            try {
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            } finally {
                lock (_lock) { _current--; }
            }
            return _messages[index];
        }
    }

    [Fact]
    public async Task SearchDmarcReportsAsync_Pop3_RespectsParallelLimitAndMaxResults() {
        var now = DateTimeOffset.UtcNow;
        var msgs = new List<MimeMessage>();
        for (int i = 0; i < 10; i++) msgs.Add(CreateDmarc("example.com", now));
        var client = new CountingPop3Client(msgs, 100);
        var maxResults = 3;
        var reports = await MailboxSearcher.SearchDmarcReportsAsync(
            client,
            domain: "example.com",
            maxResults: maxResults,
            parallelDownloadLimit: 2,
            cancellationToken: CancellationToken.None);
        Assert.Equal(maxResults, reports.Count);
        Assert.Equal(2, client.MaxConcurrency);
        Assert.True(client.CallCount <= maxResults + 1);
    }
}
