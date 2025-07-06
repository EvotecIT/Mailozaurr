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
}
