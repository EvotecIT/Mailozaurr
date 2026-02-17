using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class ImapSentMessageOperationsTests {
    [Fact]
    public async Task AppendToSentAsync_EnsuresReadWriteAndAppendsSeen() {
        var folder = new FakeImapSentFolder {
            FullNameValue = "Sent",
            IsOpenValue = true,
            AccessValue = FolderAccess.ReadOnly
        };
        var message = BuildMessage();

        var result = await ImapSentMessageOperations.AppendToSentAsync(folder, message);

        Assert.True(result.Appended);
        Assert.Equal("Sent", result.Folder);
        Assert.Equal(1, folder.CloseCount);
        Assert.Equal(1, folder.OpenCount);
        Assert.Equal(FolderAccess.ReadWrite, folder.LastOpenAccess);
        Assert.Equal(1, folder.AppendCount);
        Assert.Equal(MessageFlags.Seen, folder.LastAppendFlags);
    }

    [Fact]
    public async Task FindSentDuplicateAsync_ReturnsMatchFromIdempotencyHeaderSearch() {
        var uid = new UniqueId(123);
        var folder = new FakeImapSentFolder {
            FullNameValue = "Sent",
            IsOpenValue = true,
            AccessValue = FolderAccess.ReadOnly
        };
        folder.SearchResults.Enqueue(new List<UniqueId> { uid });
        folder.EnvelopeMessageIds[uid] = "matched@example.test";

        var result = await ImapSentMessageOperations.FindSentDuplicateAsync(
            folder,
            idempotencyHeaderName: "X-BayManager-Idempotency-Key",
            idempotencyKey: "idem-123",
            messageIdToken: "fallback@example.test");

        Assert.True(result.IsMatch);
        Assert.Equal("Sent", result.Folder);
        Assert.Equal("matched@example.test", result.MessageId);
        Assert.Single(folder.SearchQueries);
    }

    [Fact]
    public async Task FindSentDuplicateAsync_FallsBackToMessageIdSearch() {
        var uid = new UniqueId(345);
        var folder = new FakeImapSentFolder {
            FullNameValue = "Sent",
            IsOpenValue = true,
            AccessValue = FolderAccess.ReadOnly
        };
        folder.SearchResults.Enqueue(new List<UniqueId>());
        folder.SearchResults.Enqueue(new List<UniqueId> { uid });
        folder.EnvelopeMessageIds[uid] = "found-by-message-id@example.test";

        var result = await ImapSentMessageOperations.FindSentDuplicateAsync(
            folder,
            idempotencyHeaderName: "X-BayManager-Idempotency-Key",
            idempotencyKey: "idem-123",
            messageIdToken: "<msg-123@example.test>");

        Assert.True(result.IsMatch);
        Assert.Equal("found-by-message-id@example.test", result.MessageId);
        Assert.Equal(2, folder.SearchQueries.Count);
    }

    [Fact]
    public async Task FindSentDuplicateAsync_ReturnsNoneWhenNoMatch() {
        var folder = new FakeImapSentFolder {
            FullNameValue = "Sent",
            IsOpenValue = true,
            AccessValue = FolderAccess.ReadOnly
        };
        folder.SearchResults.Enqueue(new List<UniqueId>());

        var result = await ImapSentMessageOperations.FindSentDuplicateAsync(
            folder,
            idempotencyHeaderName: "X-BayManager-Idempotency-Key",
            idempotencyKey: "idem-123",
            messageIdToken: null);

        Assert.False(result.IsMatch);
        Assert.Null(result.MessageId);
        Assert.Single(folder.SearchQueries);
    }

    [Fact]
    public async Task GetThreadingMetadataAsync_ReturnsFolderMetadata() {
        var uid = new UniqueId(777);
        var folder = new FakeImapSentFolder {
            FullNameValue = "INBOX",
            IsOpenValue = false,
            AccessValue = FolderAccess.ReadOnly
        };
        folder.ThreadingMetadataByUid[uid] = new ImapSentMessageOperations.ImapThreadingMetadataResult {
            MessageId = "child@example.test",
            InReplyTo = "parent@example.test",
            ReplyTo = "reply@example.test",
            Cc = "cc@example.test",
            References = new List<string> { "root@example.test", "parent@example.test" }
        };

        var result = await ImapSentMessageOperations.GetThreadingMetadataAsync(folder, uid);

        Assert.NotNull(result);
        Assert.Equal("child@example.test", result!.MessageId);
        Assert.Equal("parent@example.test", result.InReplyTo);
        Assert.Equal("reply@example.test", result.ReplyTo);
        Assert.Equal("cc@example.test", result.Cc);
        Assert.Equal(2, result.References!.Count);
        Assert.Equal(1, folder.OpenCount);
        Assert.Equal(FolderAccess.ReadOnly, folder.LastOpenAccess);
    }

    [Theory]
    [InlineData("<abc@example.test>", "abc@example.test")]
    [InlineData(" abc@example.test ", "abc@example.test")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void NormalizeMessageIdToken_NormalizesExpectedValues(string input, string? expected) {
        var actual = ImapSentMessageOperations.NormalizeMessageIdToken(input);
        Assert.Equal(expected, actual);
    }

    private static MimeMessage BuildMessage() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.test"));
        message.To.Add(MailboxAddress.Parse("recipient@example.test"));
        message.Subject = "subject";
        message.Body = new TextPart("plain") { Text = "body" };
        return message;
    }

    private sealed class FakeImapSentFolder : ImapSentMessageOperations.IImapSentFolder {
        public string FullNameValue { get; set; } = "INBOX";
        public bool IsOpenValue { get; set; }
        public FolderAccess AccessValue { get; set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int AppendCount { get; private set; }
        public FolderAccess LastOpenAccess { get; private set; } = FolderAccess.ReadOnly;
        public MessageFlags LastAppendFlags { get; private set; } = MessageFlags.None;
        public Queue<IList<UniqueId>> SearchResults { get; } = new();
        public List<SearchQuery> SearchQueries { get; } = new();
        public Dictionary<UniqueId, string?> EnvelopeMessageIds { get; } = new();
        public Dictionary<UniqueId, ImapSentMessageOperations.ImapThreadingMetadataResult?> ThreadingMetadataByUid { get; } = new();

        public string FullName => FullNameValue;
        public bool IsOpen => IsOpenValue;
        public FolderAccess Access => AccessValue;

        public Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default) {
            OpenCount++;
            LastOpenAccess = access;
            AccessValue = access;
            IsOpenValue = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(bool expunge, CancellationToken cancellationToken = default) {
            CloseCount++;
            IsOpenValue = false;
            return Task.CompletedTask;
        }

        public Task AppendAsync(MimeMessage message, MessageFlags flags, CancellationToken cancellationToken = default) {
            _ = message ?? throw new ArgumentNullException(nameof(message));
            AppendCount++;
            LastAppendFlags = flags;
            return Task.CompletedTask;
        }

        public Task<IList<UniqueId>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default) {
            SearchQueries.Add(query);
            if (SearchResults.Count > 0) {
                return Task.FromResult(SearchResults.Dequeue());
            }
            return Task.FromResult<IList<UniqueId>>(new List<UniqueId>());
        }

        public Task<string?> FetchEnvelopeMessageIdAsync(UniqueId uid, CancellationToken cancellationToken = default) {
            EnvelopeMessageIds.TryGetValue(uid, out var value);
            return Task.FromResult(value);
        }

        public Task<ImapSentMessageOperations.ImapThreadingMetadataResult?> FetchThreadingMetadataAsync(UniqueId uid, CancellationToken cancellationToken = default) {
            ThreadingMetadataByUid.TryGetValue(uid, out var value);
            return Task.FromResult(value);
        }
    }
}
