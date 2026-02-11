using System.Collections.Generic;
using MimeKit;

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
