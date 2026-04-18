using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Moq;

namespace Mailozaurr.Tests;

public class SmtpSentFolderSessionPipelineTests {
    [Fact]
    public async Task TryFindExistingSentCopyAsync_UsesConnectedSession_AndDisconnects() {
        var connectCalls = 0;
        var resolveCalls = 0;
        var disconnectCalls = 0;

        var folder = new Mock<IMailFolder>();
        var matched = new MimeMessage { MessageId = "matched@example.test" };
        folder.SetupGet(f => f.FullName).Returns("Sent");
        folder.Setup(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId> { new(42) });
        folder.Setup(f => f.GetMessageAsync(It.IsAny<UniqueId>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
            .ReturnsAsync(matched);

        var result = await SmtpSentFolderSessionPipeline.TryFindExistingSentCopyAsync(
            connectAsync: _ => {
                connectCalls++;
                return Task.FromResult(new ImapClient());
            },
            resolveSentFolderAsync: (_, _) => {
                resolveCalls++;
                return Task.FromResult(folder.Object);
            },
            idempotencyHeaderName: "X-Test-Idempotency",
            idempotencyKey: "idem-42",
            idempotentMessageId: "fallback@example.test",
            disconnectAsync: (_, _) => {
                disconnectCalls++;
                return Task.CompletedTask;
            });

        Assert.True(result.IsMatch);
        Assert.Equal("Sent", result.Folder);
        Assert.Equal("matched@example.test", result.MessageId);
        Assert.Equal(1, connectCalls);
        Assert.Equal(1, resolveCalls);
        Assert.Equal(1, disconnectCalls);
    }

    [Fact]
    public async Task TryAppendToSentAsync_UsesConnectedSession_AndDisconnects() {
        var connectCalls = 0;
        var resolveCalls = 0;
        var appendCalls = 0;
        var disconnectCalls = 0;

        var folder = new Mock<IMailFolder>();
        folder.SetupGet(f => f.FullName).Returns("Sent Items");
        folder.SetupGet(f => f.IsOpen).Returns(true);
        folder.SetupGet(f => f.Access).Returns(FolderAccess.ReadWrite);

        var result = await SmtpSentFolderSessionPipeline.TryAppendToSentAsync(
            connectAsync: _ => {
                connectCalls++;
                return Task.FromResult(new ImapClient());
            },
            resolveSentFolderAsync: (_, _) => {
                resolveCalls++;
                return Task.FromResult(folder.Object);
            },
            message: new MimeMessage(),
            flags: MessageFlags.Seen,
            appendAsync: (_, _, _, _) => {
                appendCalls++;
                return Task.CompletedTask;
            },
            disconnectAsync: (_, _) => {
                disconnectCalls++;
                return Task.CompletedTask;
            });

        Assert.True(result.Appended);
        Assert.Equal("Sent Items", result.Folder);
        Assert.Null(result.Error);
        Assert.Equal(1, connectCalls);
        Assert.Equal(1, resolveCalls);
        Assert.Equal(1, appendCalls);
        Assert.Equal(1, disconnectCalls);
    }

    [Fact]
    public async Task TryAppendToSentAsync_Throws_ForInvalidArguments() {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            SmtpSentFolderSessionPipeline.TryAppendToSentAsync(
                connectAsync: null!,
                resolveSentFolderAsync: (_, _) => Task.FromResult(Mock.Of<IMailFolder>()),
                message: new MimeMessage()));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            SmtpSentFolderSessionPipeline.TryAppendToSentAsync(
                connectAsync: _ => Task.FromResult(new ImapClient()),
                resolveSentFolderAsync: null!,
                message: new MimeMessage()));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            SmtpSentFolderSessionPipeline.TryAppendToSentAsync(
                connectAsync: _ => Task.FromResult(new ImapClient()),
                resolveSentFolderAsync: (_, _) => Task.FromResult(Mock.Of<IMailFolder>()),
                message: null!));
    }

    [Fact]
    public async Task TryGetThreadingMetadataAsync_UsesConnectedSession_AndDisconnects() {
        var connectCalls = 0;
        var metadataCalls = 0;
        var disconnectCalls = 0;
        var expected = new ImapSentMessageOperations.ImapThreadingMetadataResult {
            MessageId = "child@example.test",
            InReplyTo = "parent@example.test"
        };

        var actual = await SmtpSentFolderSessionPipeline.TryGetThreadingMetadataAsync(
            connectAsync: _ => {
                connectCalls++;
                return Task.FromResult(new ImapClient());
            },
            folder: "INBOX",
            uid: 42,
            getMetadataAsync: (_, folder, uid, _) => {
                metadataCalls++;
                Assert.Equal("INBOX", folder);
                Assert.Equal((uint)42, uid);
                return Task.FromResult<ImapSentMessageOperations.ImapThreadingMetadataResult?>(expected);
            },
            disconnectAsync: (_, _) => {
                disconnectCalls++;
                return Task.CompletedTask;
            });

        Assert.Same(expected, actual);
        Assert.Equal(1, connectCalls);
        Assert.Equal(1, metadataCalls);
        Assert.Equal(1, disconnectCalls);
    }

    [Fact]
    public async Task TryGetThreadingMetadataAsync_Throws_ForInvalidArguments() {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            SmtpSentFolderSessionPipeline.TryGetThreadingMetadataAsync(
                connectAsync: null!,
                folder: "INBOX",
                uid: 1));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SmtpSentFolderSessionPipeline.TryGetThreadingMetadataAsync(
                connectAsync: _ => Task.FromResult(new ImapClient()),
                folder: " ",
                uid: 1));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            SmtpSentFolderSessionPipeline.TryGetThreadingMetadataAsync(
                connectAsync: _ => Task.FromResult(new ImapClient()),
                folder: "INBOX",
                uid: 0));
    }
}
