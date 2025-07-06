using Xunit;

namespace Mailozaurr.Tests;

public class GraphInboxRulesTests {
    [Fact]
    public void JoinUriQuery_BuildsRulesUri() {
        string uri = MicrosoftGraphUtils.JoinUriQuery(
            "https://graph.microsoft.com/v1.0",
            "/users/user@example.com/mailFolders/inbox/messageRules");
        Assert.Equal("https://graph.microsoft.com/v1.0/users/user@example.com/mailFolders/inbox/messageRules", uri);
    }

    [Fact]
    public void JoinUriQuery_BuildsRuleIdUri() {
        string uri = MicrosoftGraphUtils.JoinUriQuery(
            "https://graph.microsoft.com/v1.0",
            "/users/user@example.com/mailFolders/inbox/messageRules/1");
        Assert.Equal("https://graph.microsoft.com/v1.0/users/user@example.com/mailFolders/inbox/messageRules/1", uri);
    }

    [Fact]
    public void JoinUriQuery_BuildsFilteredUri() {
        string uri = MicrosoftGraphUtils.JoinUriQuery(
            "https://graph.microsoft.com/v1.0",
            "/users/user@example.com/mailFolders/inbox/messageRules",
            new Dictionary<string, object> { ["$filter"] = "displayName eq 'A'" });
        Assert.Equal("https://graph.microsoft.com/v1.0/users/user@example.com/mailFolders/inbox/messageRules?%24filter=displayName%20eq%20%27A%27", uri);
    }

    [Fact]
    public void Builder_CreatesRule() {
        var rule = new GraphInboxRuleBuilder()
            .DisplayName("Test")
            .Sequence(1)
            .SenderContains("a@example.com")
            .Build();
        Assert.Equal("Test", rule.DisplayName);
        Assert.Equal(1, rule.Sequence);
        Assert.NotNull(rule.Conditions);
        Assert.Contains("a@example.com", rule.Conditions!.SenderContains!);
    }
}
