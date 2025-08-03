using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MailKit.Search;
using MailKit.Net.Pop3;
using MailKit;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class SearchNonDeliveryReportsTests {
    private static MimeMessage CreateNdr(string recipient, string messageId, DateTimeOffset date) {
        string raw = $"Content-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\r\n\r\n--XXX\r\nContent-Type: text/plain; charset=utf-8\r\n\r\ntext\r\n\r\n--XXX\r\nContent-Type: message/delivery-status\r\n\r\nOriginal-Recipient: rfc822; {recipient}\r\nFinal-Recipient: rfc822; {recipient}\r\nOriginal-Message-ID: {messageId}\r\nReporting-MTA: dns; mx.example.com\r\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\r\nStatus: 5.1.1\r\nArrival-Date: {date:R}\r\n\r\n--XXX--";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        return MimeMessage.Load(stream);
    }

    [Fact]
    public void FilterNonDeliveryReports_FiltersByRecipientAndMessageId() {
        var now = DateTimeOffset.UtcNow;
        var msg1 = CreateNdr("user@example.com", "<id1>", now);
        var msg2 = CreateNdr("other@example.com", "<id2>", now);
        var list = new List<MimeMessage> { msg1, msg2 };
        var reports = MailboxSearcher.FilterNonDeliveryReports(
            list,
            since: now.AddMinutes(-5).DateTime,
            before: now.AddMinutes(5).DateTime,
            recipientContains: "user@example.com",
            messageId: "<id1>");
        Assert.Single(reports);
        Assert.Equal("<id1>", reports[0].OriginalMessageId);
    }

    [Fact]
    public void FilterNonDeliveryReports_FiltersByDate() {
        var now = DateTimeOffset.UtcNow;
        var msg1 = CreateNdr("user@example.com", "<id1>", now.AddMinutes(-10));
        var msg2 = CreateNdr("user@example.com", "<id2>", now);
        var list = new List<MimeMessage> { msg1, msg2 };
        var reports = MailboxSearcher.FilterNonDeliveryReports(list, since: now.AddMinutes(-5).DateTime, before: null, recipientContains: null, messageId: null);
        Assert.Single(reports);
        Assert.Equal("<id2>", reports[0].OriginalMessageId);
    }

    private static bool Contains(SearchQuery query, Func<SearchQuery, bool> predicate) {
        if (predicate(query)) return true;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var leftProp = query.GetType().GetProperty("Left", flags);
        var rightProp = query.GetType().GetProperty("Right", flags);
        var left = leftProp?.GetValue(query) as SearchQuery;
        var right = rightProp?.GetValue(query) as SearchQuery;
        if (left != null && Contains(left, predicate)) return true;
        if (right != null && Contains(right, predicate)) return true;
        return false;
    }

    [Fact]
    public void BuildNonDeliveryReportSearchQuery_ContainsFilters() {
        var query = MailboxSearcher.BuildNonDeliveryReportSearchQuery(null, null);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        bool hasHeader = Contains(query, q => {
            var term = q.GetType().GetProperty("Term", flags)?.GetValue(q)?.ToString();
            if (term == "HeaderContains") {
                var field = q.GetType().GetProperty("Field", flags)?.GetValue(q)?.ToString();
                var value = q.GetType().GetProperty("Value", flags)?.GetValue(q)?.ToString();
                return string.Equals(field, "Content-Type", StringComparison.OrdinalIgnoreCase) &&
                    value?.IndexOf("delivery-status", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        });
        Assert.True(hasHeader);
        bool hasSubject = Contains(query, q => {
            var term = q.GetType().GetProperty("Term", flags)?.GetValue(q)?.ToString();
            if (term == "SubjectContains") {
                var text = q.GetType().GetProperty("Text", flags)?.GetValue(q)?.ToString();
                return text?.IndexOf("Mail delivery failed", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        });
        Assert.True(hasSubject);
    }

    private class FakePop3Client : Pop3Client {
        private readonly List<MimeMessage> _messages;
        private readonly int _delay;
        public FakePop3Client(IEnumerable<MimeMessage> messages, int delay) {
            _messages = new List<MimeMessage>(messages);
            _delay = delay;
        }
        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => _messages.Count;
        public override async Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            return _messages[index];
        }
    }

    [Fact]
    public async Task SearchNonDeliveryReportsAsync_Pop3_DownloadsInParallel() {
        var now = DateTimeOffset.UtcNow;
        var msgs = new List<MimeMessage>();
        for (int i = 0; i < 4; i++) msgs.Add(CreateNdr($"u{i}@example.com", $"<id{i}>", now));
        var client = new FakePop3Client(msgs, 500);
        var seq = Stopwatch.StartNew();
        _ = await MailboxSearcher.SearchNonDeliveryReportsAsync(
            client,
            parallelDownloadLimit: 0,
            cancellationToken: CancellationToken.None);
        seq.Stop();
        var sw = Stopwatch.StartNew();
        var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
            client,
            parallelDownloadLimit: 4,
            cancellationToken: CancellationToken.None);
        sw.Stop();
        Assert.Equal(4, reports.Count);
        Assert.True(sw.Elapsed < seq.Elapsed, $"sequential: {seq.ElapsedMilliseconds}, parallel: {sw.ElapsedMilliseconds}");
    }
}
