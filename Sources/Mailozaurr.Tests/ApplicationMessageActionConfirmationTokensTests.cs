using Mailozaurr.Application;
using Xunit;

namespace Mailozaurr.Tests;

public class ApplicationMessageActionConfirmationTokensTests {
    [Fact]
    public void CreateDeleteTokenPreservesMessageIdCasing() {
        var lowerToken = MessageActionConfirmationTokens.CreateDeleteToken(
            "work-imap",
            "shared@example.com",
            "Inbox",
            new[] { "abc123" });

        var upperToken = MessageActionConfirmationTokens.CreateDeleteToken(
            "work-imap",
            "shared@example.com",
            "Inbox",
            new[] { "ABC123" });

        Assert.NotEqual(lowerToken, upperToken);
    }

    [Fact]
    public void CreateDeleteTokenIgnoresMessageIdOrdering() {
        var firstToken = MessageActionConfirmationTokens.CreateDeleteToken(
            "work-imap",
            "shared@example.com",
            "Inbox",
            new[] { "msg-b", "msg-a" });

        var secondToken = MessageActionConfirmationTokens.CreateDeleteToken(
            "work-imap",
            "shared@example.com",
            "Inbox",
            new[] { "msg-a", "msg-b" });

        Assert.Equal(firstToken, secondToken);
    }
}