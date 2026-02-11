using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
using MimeKit;
using Moq;

namespace Mailozaurr.Tests;

public class SmtpSendPipelineTests {
    [Fact]
    public void ApplyThreadingHeaders_SetsNormalizedMessageThreadAndIdempotencyHeaders() {
        var message = new MimeMessage();
        var references = new List<string?> { "ref-1@example.test", " <ref-1@example.test> ", "ref-2@example.test" };

        SmtpSendPipeline.ApplyThreadingHeaders(
            message,
            inReplyToCandidate: "in-reply@example.test",
            referenceCandidates: references,
            messageId: "message-id@example.test",
            idempotencyHeaderName: "X-Test-Idempotency",
            idempotencyKey: "idem-1");

        Assert.Equal("idem-1", message.Headers["X-Test-Idempotency"]);
        Assert.Equal("message-id@example.test", TrimBrackets(message.MessageId));
        Assert.Equal("in-reply@example.test", TrimBrackets(message.InReplyTo));

        var refs = Canonicalize(message.References);
        Assert.Contains("ref-1@example.test", refs);
        Assert.Contains("ref-2@example.test", refs);
        Assert.Contains("in-reply@example.test", refs);
        Assert.Equal(3, refs.Count);
    }

    [Fact]
    public void ApplyThreadingHeaders_Throws_WhenMessageIsNull() {
        Assert.Throws<ArgumentNullException>(() =>
            SmtpSendPipeline.ApplyThreadingHeaders(
                message: null!,
                inReplyToCandidate: null,
                referenceCandidates: null));
    }

    [Fact]
    public async Task TryFindExistingSentCopyAsync_ReturnsMatch_FromIdempotencyHeaderProbe() {
        var folder = new Mock<IMailFolder>();
        var matched = new MimeMessage { MessageId = "matched-101@example.test" };
        folder.SetupGet(f => f.FullName).Returns("Sent");
        folder.Setup(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId> { new(101) });
        folder.Setup(f => f.GetMessageAsync(It.IsAny<UniqueId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matched);

        var result = await SmtpSendPipeline.TryFindExistingSentCopyAsync(
            folder.Object,
            idempotencyHeaderName: "X-Test-Idempotency",
            idempotencyKey: "idem-101",
            idempotentMessageId: "fallback@example.test");

        Assert.True(result.IsMatch);
        Assert.Equal("Sent", result.Folder);
        Assert.Equal("matched-101@example.test", result.MessageId);
        folder.Verify(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryFindExistingSentCopyAsync_FallsBackToMessageIdProbe_WhenHeaderProbeMisses() {
        var folder = new Mock<IMailFolder>();
        var matched = new MimeMessage { MessageId = "matched-202@example.test" };
        folder.SetupGet(f => f.FullName).Returns("Sent Items");
        folder.SetupSequence(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId>())
            .ReturnsAsync(new List<UniqueId> { new(202) });
        folder.Setup(f => f.GetMessageAsync(It.IsAny<UniqueId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matched);

        var result = await SmtpSendPipeline.TryFindExistingSentCopyAsync(
            folder.Object,
            idempotencyHeaderName: "X-Test-Idempotency",
            idempotencyKey: "idem-202",
            idempotentMessageId: "<message-202@example.test>");

        Assert.True(result.IsMatch);
        Assert.Equal("Sent Items", result.Folder);
        Assert.Equal("matched-202@example.test", result.MessageId);
        folder.Verify(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TryFindExistingSentCopyAsync_ReturnsNone_WhenProbeThrows() {
        var folder = new Mock<IMailFolder>();
        folder.Setup(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("search failed"));

        var result = await SmtpSendPipeline.TryFindExistingSentCopyAsync(
            folder.Object,
            idempotencyHeaderName: "X-Test-Idempotency",
            idempotencyKey: "idem-500",
            idempotentMessageId: "fallback@example.test");

        Assert.False(result.IsMatch);
        Assert.Null(result.Folder);
        Assert.Null(result.MessageId);
    }

    [Fact]
    public async Task TryFindExistingSentCopyAsync_Throws_ForInvalidArguments() {
        var folder = new Mock<IMailFolder>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            SmtpSendPipeline.TryFindExistingSentCopyAsync(
                sentFolder: null!,
                idempotencyHeaderName: "X-Test-Idempotency",
                idempotencyKey: "idem",
                idempotentMessageId: null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SmtpSendPipeline.TryFindExistingSentCopyAsync(
                folder.Object,
                idempotencyHeaderName: "",
                idempotencyKey: "idem",
                idempotentMessageId: null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SmtpSendPipeline.TryFindExistingSentCopyAsync(
                folder.Object,
                idempotencyHeaderName: "X-Test-Idempotency",
                idempotencyKey: "",
                idempotentMessageId: null));
    }

    private static List<string> Canonicalize(IEnumerable<string> values) {
        var output = new List<string>();
        foreach (var value in values) {
            output.Add(TrimBrackets(value));
        }
        return output;
    }

    private static string TrimBrackets(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }
        return value.Trim().Trim('<', '>');
    }
}
