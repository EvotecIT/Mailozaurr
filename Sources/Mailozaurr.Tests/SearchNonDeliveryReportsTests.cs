using Mailozaurr;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using MailKit.Search;
using MailKit.Net.Pop3;
using MailKit;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class SearchNonDeliveryReportsTests {
    private static MimeMessage CreateNdr(string recipient, string messageId, DateTimeOffset date, DateTimeOffset? lastAttempt = null, bool includeArrival = true) {
        string arrival = date.ToString("ddd, dd MMM yyyy HH:mm:ss K", CultureInfo.InvariantCulture);
        string? lastAttemptStr = lastAttempt?.ToString("ddd, dd MMM yyyy HH:mm:ss K", CultureInfo.InvariantCulture);
        string raw = $"Date: {arrival}\r\nContent-Type: multipart/report; report-type=delivery-status; boundary=\"XXX\"\r\n\r\n--XXX\r\nContent-Type: text/plain; charset=utf-8\r\n\r\ntext\r\n\r\n--XXX\r\nContent-Type: message/delivery-status\r\n\r\nOriginal-Recipient: rfc822; {recipient}\r\nFinal-Recipient: rfc822; {recipient}\r\nOriginal-Message-ID: {messageId}\r\nReporting-MTA: dns; mx.example.com\r\nDiagnostic-Code: smtp; 550 5.1.1 User unknown\r\nStatus: 5.1.1\r\n";
        if (includeArrival) raw += $"Arrival-Date: {arrival}\r\n";
        if (lastAttemptStr != null) raw += $"Last-Attempt-Date: {lastAttemptStr}\r\n";
        raw += "\r\n--XXX--";
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

    [Fact]
    public void FilterNonDeliveryReports_UsesLastAttemptDateWhenPresent() {
        var now = DateTimeOffset.UtcNow;
        var msg = CreateNdr("user@example.com", "<id1>", now.AddDays(-2), lastAttempt: now);
        var list = new List<MimeMessage> { msg };
        var reports = MailboxSearcher.FilterNonDeliveryReports(list, since: now.AddHours(-1).DateTime, before: null, recipientContains: null, messageId: null);
        Assert.Single(reports);
        Assert.Equal("<id1>", reports[0].OriginalMessageId);
    }

    [Fact]
    public void FilterNonDeliveryReports_FiltersAcrossTimeZones() {
        var msg1 = CreateNdr("user@example.com", "<id1>", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)));
        var msg2 = CreateNdr("user@example.com", "<id2>", new DateTimeOffset(2024, 1, 1, 5, 0, 0, TimeSpan.FromHours(-5)));
        var list = new List<MimeMessage> { msg1, msg2 };
        var since = new DateTime(2023, 12, 31, 21, 0, 0, DateTimeKind.Utc);
        var before = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var reports = MailboxSearcher.FilterNonDeliveryReports(list, since, before, recipientContains: null, messageId: null);
        Assert.Single(reports);
        Assert.Equal("<id1>", reports[0].OriginalMessageId);
    }

    [Fact]
    public void FilterNonDeliveryReports_SubjectDetectedReportSurvivesDateFilter() {
        var now = DateTimeOffset.UtcNow;
        var date = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Offset);
        string raw = $"Date: {date:R}\r\nSubject: Mail Delivery Subsystem\r\n\r\ntext";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var message = MimeMessage.Load(stream);
        var list = new List<MimeMessage> { message };
        var since = date.AddMinutes(-5).UtcDateTime;
        var before = date.AddMinutes(5).UtcDateTime;
        var reports = MailboxSearcher.FilterNonDeliveryReports(list, since, before, recipientContains: null, messageId: null);
        Assert.Single(reports);
        Assert.Equal(date, reports[0].Timestamp);
    }

    [Fact]
    public void FilterNonDeliveryReports_ReportWithoutTimestampUsesMessageDate() {
        var now = DateTimeOffset.UtcNow;
        now = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Offset);
        var msg = CreateNdr("user@example.com", "<id1>", now, includeArrival: false);
        var list = new List<MimeMessage> { msg };
        var since = now.AddMinutes(-5).UtcDateTime;
        var before = now.AddMinutes(5).UtcDateTime;
        var reports = MailboxSearcher.FilterNonDeliveryReports(list, since, before, recipientContains: null, messageId: null);
        Assert.Single(reports);
        Assert.Equal(now, reports[0].Timestamp);
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

    private class CountingPop3Client : Pop3Client {
        private readonly List<MimeMessage> _messages;
        private readonly int _delay;
        private readonly object _lock = new();
        private int _current;
        public int MaxConcurrency { get; private set; }
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

    [Fact]
    public async Task SearchNonDeliveryReportsAsync_Pop3_RespectsParallelLimit() {
        var now = DateTimeOffset.UtcNow;
        var msgs = new List<MimeMessage>();
        for (int i = 0; i < 20; i++) msgs.Add(CreateNdr($"u{i}@example.com", $"<id{i}>", now));
        var client = new CountingPop3Client(msgs, 100);
        var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
            client,
            parallelDownloadLimit: 4,
            cancellationToken: CancellationToken.None);
        Assert.Equal(msgs.Count, reports.Count);
        Assert.Equal(4, client.MaxConcurrency);
    }

    [Fact]
    public async Task SearchNonDeliveryReportsAsync_Pop3_StopsAfterMaxResults() {
        var now = DateTimeOffset.UtcNow;
        var msgs = new List<MimeMessage>();
        msgs.Add(new MimeMessage());
        msgs.Add(CreateNdr("u1@example.com", "<id1>", now));
        msgs.Add(CreateNdr("u2@example.com", "<id2>", now));
        msgs.Add(CreateNdr("u3@example.com", "<id3>", now));
        var client = new TrackingPop3Client(msgs);
        var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
            client,
            maxResults: 2,
            parallelDownloadLimit: 2,
            cancellationToken: CancellationToken.None);
        Assert.Equal(2, reports.Count);
        Assert.True(client.FetchCount <= 4, $"fetched {client.FetchCount}");
    }

    [Fact]
    public async Task SearchNonDeliveryReportsAsync_Graph_FiltersBySubject() {
        var handler = new NdrHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
            var reports = await MailboxSearcher.SearchNonDeliveryReportsAsync(
                cred,
                "user@example.com",
                cancellationToken: CancellationToken.None);
            Assert.Single(reports);
            Assert.Equal(1, handler.MimeFetches);
            Assert.NotNull(handler.Filter);
            foreach (var pattern in NonDeliveryReportSubjectPatterns.Values) {
                Assert.Contains(pattern, handler.Filter!);
            }
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    private sealed class NdrHandler : HttpMessageHandler {
        public string? Filter { get; private set; }
        public int MimeFetches { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var uri = request.RequestUri!;
            if (uri.AbsoluteUri.Contains("oauth2")) {
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }

            if (uri.AbsolutePath.EndsWith("/messages")) {
                var query = uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var q in query) {
                    var parts = q.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == "$filter") Filter = Uri.UnescapeDataString(parts[1]);
                }
                var json = "{\"value\":[{\"id\":\"1\"}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }

            if (uri.AbsolutePath.Contains("/messages/") && uri.AbsolutePath.EndsWith("/$value")) {
                MimeFetches++;
                const string raw = "Date: Mon, 1 Jan 2024 00:00:00 +0000\r\nSubject: Mail Delivery Subsystem\r\n\r\nbody";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(raw) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
