using Mailozaurr;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mailozaurr.Tests;

public class MicrosoftGraphUtilsTests {
    [Fact]
    public void BuildGraphUri_JoinsBaseAndPath() {
        string uri = MicrosoftGraphUtils.BuildGraphUri(GraphEndpoint.V1, "/users/me");
        Assert.Equal("https://graph.microsoft.com/v1.0/users/me", uri);
    }

    [Fact]
    public void BuildGraphUri_EncodesQueryParameters() {
        var uri = MicrosoftGraphUtils.BuildGraphUri("https://api.example.com", "search", new Dictionary<string, string> { { "q", "a b" } });
        Assert.Equal("https://api.example.com/search?q=a%20b", uri);
    }

    [Fact]
    public void BuildGraphPath_EscapesEveryDynamicSegment() {
        string path = MicrosoftGraphUtils.BuildGraphPath(
            "users",
            "user@example.com/messages/other?x=1#fragment",
            "messages",
            "AAMk+/=%value");

        Assert.Equal(
            "/users/user%40example.com%2Fmessages%2Fother%3Fx%3D1%23fragment/messages/AAMk%2B%2F%3D%25value",
            path);
        Assert.DoesNotContain("?", path, StringComparison.Ordinal);
        Assert.DoesNotContain("#", path, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildGraphPath_RejectsEmptySegments() {
        Assert.Throws<ArgumentException>(() => MicrosoftGraphUtils.BuildGraphPath("users", string.Empty));
    }

    [Fact]
    public void JoinUriQuery_JoinsUriCorrectly() {
        string uri = MicrosoftGraphUtils.JoinUriQuery(GraphEndpoint.V1, "users/me");
        Assert.Equal("https://graph.microsoft.com/v1.0/users/me", uri);
    }

    [Fact]
    public void JoinUriQuery_EncodesQueryParameters() {
        var uri = MicrosoftGraphUtils.JoinUriQuery("https://example.com", "path", new Dictionary<string, object> { { "filter", "a&b" } });
        Assert.Equal("https://example.com/path?filter=a%26b", uri);
    }

    [Fact]
    public void RemoveEmptyValues_RemovesNestedEmptyEntries() {
        var dict = new Dictionary<string, object> {
            ["keep"] = "value",
            ["empty"] = "",
            ["nested"] = new Dictionary<string, object> { ["sub"] = "" }
        };

        MicrosoftGraphUtils.RemoveEmptyValues(dict);

        Assert.True(dict.ContainsKey("keep"));
        Assert.False(dict.ContainsKey("empty"));
        Assert.False(dict.ContainsKey("nested"));
    }

    [Fact]
    public void RemoveEmptyValues_RespectsExcludeParameter() {
        var dict = new Dictionary<string, object> {
            ["remove"] = "",
            ["keep"] = ""
        };
        var exclude = new HashSet<string> { "keep" };

        MicrosoftGraphUtils.RemoveEmptyValues(dict, exclude);

        Assert.False(dict.ContainsKey("remove"));
        Assert.True(dict.ContainsKey("keep"));
    }

    [Fact]
    public void FilterJunkMessages_SkipId() {
        var msg1 = new Dictionary<string, object> { ["id"] = "1" };
        var msg2 = new Dictionary<string, object> { ["id"] = "2" };
        var filtered = MicrosoftGraphUtils.FilterJunkMessages(new[] { msg1, msg2 }, skipIds: new[] { "1" });
        Assert.DoesNotContain(msg1, filtered);
        Assert.Contains(msg2, filtered);
    }

    [Fact]
    public void FilterJunkMessages_SkipFromToSubjectAndAttachment() {
        var msg1 = new Dictionary<string, object> {
            ["id"] = "1",
            ["from"] = new Dictionary<string, object> {
                ["emailAddress"] = new Dictionary<string, object> { ["address"] = "sender@example.com" }
            },
            ["toRecipients"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["emailAddress"] = new Dictionary<string, object> { ["address"] = "rcpt@example.com" }
                }
            },
            ["subject"] = "Urgent meeting",
            ["hasAttachments"] = true
        };

        var filtered = MicrosoftGraphUtils.FilterJunkMessages(
            new[] { msg1 },
            skipFrom: new[] { "sender@example.com" },
            skipTo: new[] { "rcpt@example.com" },
            skipSubjectContains: new[] { "urgent" },
            skipHasAttachment: true);

        Assert.Empty(filtered);
    }
}
