using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class Pop3MailboxBrowserTests {
    [Fact]
    public async Task ListMessageHeadersCoreAsync_ListsNewestFirst_WithOffsetAndLimit() {
        var fake = new FakePop3Client(count: 5);
        // indices: 0..4, newest is 4
        fake.SetHeaders(4, BuildHeaders(messageId: "<m4>", from: "f4", to: "t4", subject: "s4", date: "Mon, 01 Jan 2024 10:00:00 +0000"));
        fake.SetHeaders(3, BuildHeaders(messageId: "<m3>", from: "f3", to: "t3", subject: "s3", date: "Mon, 01 Jan 2024 09:00:00 +0000"));
        fake.SetHeaders(2, BuildHeaders(messageId: "<m2>", from: "f2", to: "t2", subject: "s2", date: "Mon, 01 Jan 2024 08:00:00 +0000"));
        fake.SetHeaders(1, BuildHeaders(messageId: "<m1>", from: "f1", to: "t1", subject: "s1", date: "Mon, 01 Jan 2024 07:00:00 +0000"));
        fake.SetHeaders(0, BuildHeaders(messageId: "<m0>", from: "f0", to: "t0", subject: "s0", date: "Mon, 01 Jan 2024 06:00:00 +0000"));

        fake.SetUid(4, "u4");
        fake.SetUid(3, "u3");
        fake.SetUid(2, "u2");
        fake.SetUid(1, "u1");
        fake.SetUid(0, "u0");

        fake.SetSize(4, 400);
        fake.SetSize(3, 300);
        fake.SetSize(2, 200);
        fake.SetSize(1, 100);
        fake.SetSize(0, 0);

        var list = await Pop3MailboxBrowser.ListMessageHeadersCoreAsync(fake, limit: 2, offset: 1, CancellationToken.None);
        Assert.Equal(5, list.TotalCount);
        Assert.Equal(2, list.Messages.Count);

        // offset=1 skips newest (4), so first is 3 then 2.
        Assert.Equal(3, list.Messages[0].Index);
        Assert.Equal("u3", list.Messages[0].Uid);
        Assert.Equal(300, list.Messages[0].MessageSize);
        Assert.Equal("m3", list.Messages[0].MessageId);
        Assert.Equal("f3", list.Messages[0].From);
        Assert.Equal("t3", list.Messages[0].To);
        Assert.Equal("s3", list.Messages[0].Subject);

        Assert.Equal(2, list.Messages[1].Index);
        Assert.Equal("m2", list.Messages[1].MessageId);
    }

    [Fact]
    public async Task ResolveMessageCoreAsync_MissingIdentifier_ReturnsMissingIdentifier() {
        var fake = new FakePop3Client(count: 1);
        var result = await Pop3MailboxBrowser.ResolveMessageCoreAsync(fake, requestedIndex: null, requestedUid: null, CancellationToken.None);
        Assert.Equal(Pop3MailboxBrowser.Pop3MessageResolveStatus.MissingIdentifier, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task ResolveMessageCoreAsync_InvalidIndex_ReturnsInvalidIndex() {
        var fake = new FakePop3Client(count: 1);
        var result = await Pop3MailboxBrowser.ResolveMessageCoreAsync(fake, requestedIndex: -1, requestedUid: null, CancellationToken.None);
        Assert.Equal(Pop3MailboxBrowser.Pop3MessageResolveStatus.InvalidIndex, result.Status);
    }

    [Fact]
    public async Task ResolveMessageCoreAsync_UidLookupUnsupported_ReturnsUidLookupUnsupported() {
        var fake = new FakePop3Client(count: 1) { ThrowUidListNotSupported = true };
        var result = await Pop3MailboxBrowser.ResolveMessageCoreAsync(fake, requestedIndex: null, requestedUid: "u1", CancellationToken.None);
        Assert.Equal(Pop3MailboxBrowser.Pop3MessageResolveStatus.UidLookupUnsupported, result.Status);
    }

    [Fact]
    public async Task ResolveMessageCoreAsync_ResolvesByUid() {
        var fake = new FakePop3Client(count: 3);
        fake.Uids = new List<string> { "u0", "u1", "u2" };
        fake.SetSize(1, 123);
        fake.SetMessage(1, new MimeMessage { Subject = "x" });

        var result = await Pop3MailboxBrowser.ResolveMessageCoreAsync(fake, requestedIndex: null, requestedUid: "u1", CancellationToken.None);
        Assert.Equal(Pop3MailboxBrowser.Pop3MessageResolveStatus.Success, result.Status);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(1, result.Snapshot!.Index);
        Assert.Equal("u1", result.Snapshot.Uid);
        Assert.Equal(123, result.Snapshot.MessageSize);
        Assert.Equal("x", result.Snapshot.Message.Subject);
    }

    [Fact]
    public void NormalizeMessageIdValue_StripsAngleBrackets() {
        Assert.Equal("x@y", Pop3MailboxBrowser.NormalizeMessageIdValue("<x@y>"));
        Assert.Equal("x@y", Pop3MailboxBrowser.NormalizeMessageIdValue("x@y"));
        Assert.Null(Pop3MailboxBrowser.NormalizeMessageIdValue("   "));
    }

    private static HeaderList BuildHeaders(string messageId, string from, string to, string subject, string date) {
        var headers = new HeaderList();
        headers.Add(HeaderId.MessageId, messageId);
        headers.Add(HeaderId.From, from);
        headers.Add(HeaderId.To, to);
        headers.Add(HeaderId.Subject, subject);
        headers.Add(HeaderId.Date, date);
        return headers;
    }

    private sealed class FakePop3Client : Pop3MailboxBrowser.IPop3MailboxClient {
        private readonly Dictionary<int, HeaderList> _headers = new();
        private readonly Dictionary<int, string> _uids = new();
        private readonly Dictionary<int, long> _sizes = new();
        private readonly Dictionary<int, MimeMessage> _messages = new();

        internal FakePop3Client(int count) {
            Count = count;
        }

        public int Count { get; }
        public bool ThrowUidListNotSupported { get; set; }
        public IList<string> Uids { get; set; } = new List<string>();

        internal void SetHeaders(int index, HeaderList headers) => _headers[index] = headers;
        internal void SetUid(int index, string uid) => _uids[index] = uid;
        internal void SetSize(int index, long size) => _sizes[index] = size;
        internal void SetMessage(int index, MimeMessage msg) => _messages[index] = msg;

        public Task<HeaderList> GetMessageHeadersAsync(int index, CancellationToken cancellationToken) =>
            Task.FromResult(_headers.TryGetValue(index, out var h) ? h : new HeaderList());

        public Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken) =>
            Task.FromResult(_messages.TryGetValue(index, out var m) ? m : new MimeMessage());

        public Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken) =>
            Task.FromResult(_uids.TryGetValue(index, out var u) ? u : $"u{index}");

        public Task<IList<string>> GetMessageUidsAsync(CancellationToken cancellationToken) {
            if (ThrowUidListNotSupported) {
                throw new NotSupportedException("uid list not supported");
            }
            return Task.FromResult(Uids);
        }

        public long GetMessageSize(int index, CancellationToken cancellationToken) =>
            _sizes.TryGetValue(index, out var s) ? s : 0;
    }
}

