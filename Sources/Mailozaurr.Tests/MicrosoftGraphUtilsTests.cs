using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Mailozaurr;

namespace Mailozaurr.Tests;

public class MicrosoftGraphUtilsTests
{
    [Fact]
    public void BuildGraphUri_JoinsBaseAndPath()
    {
        string uri = MicrosoftGraphUtils.BuildGraphUri("https://graph.microsoft.com/v1.0/", "/users/me");
        Assert.Equal("https://graph.microsoft.com/v1.0/users/me", uri);
    }

    [Fact]
    public void BuildGraphUri_EncodesQueryParameters()
    {
        var uri = MicrosoftGraphUtils.BuildGraphUri("https://api.example.com", "search", new Dictionary<string, string> { { "q", "a b" } });
        Assert.Equal("https://api.example.com/search?q=a%20b", uri);
    }

    [Fact]
    public void JoinUriQuery_JoinsUriCorrectly()
    {
        string uri = MicrosoftGraphUtils.JoinUriQuery("https://graph.microsoft.com/v1.0/", "users/me");
        Assert.Equal("https://graph.microsoft.com/v1.0/users/me", uri);
    }

    [Fact]
    public void JoinUriQuery_EncodesQueryParameters()
    {
        var uri = MicrosoftGraphUtils.JoinUriQuery("https://example.com", "path", new Dictionary<string, object> { { "filter", "a&b" } });
        Assert.Equal("https://example.com/path?filter=a%26b", uri);
    }

    [Fact]
    public void RemoveEmptyValues_RemovesNestedEmptyEntries()
    {
        var dict = new Dictionary<string, object>
        {
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
    public void RemoveEmptyValues_RespectsExcludeParameter()
    {
        var dict = new Dictionary<string, object>
        {
            ["remove"] = "",
            ["keep"] = ""
        };
        var exclude = new HashSet<string> { "keep" };

        MicrosoftGraphUtils.RemoveEmptyValues(dict, exclude);

        Assert.False(dict.ContainsKey("remove"));
        Assert.True(dict.ContainsKey("keep"));
    }

    [Fact]
    public void FilterJunkMessages_SkipId()
    {
        var msg1 = new Dictionary<string, object> { ["id"] = "1" };
        var msg2 = new Dictionary<string, object> { ["id"] = "2" };
        var filtered = MicrosoftGraphUtils.FilterJunkMessages(new[] { msg1, msg2 }, skipIds: new[] { "1" });
        Assert.DoesNotContain(msg1, filtered);
        Assert.Contains(msg2, filtered);
    }

    [Fact]
    public void FilterJunkMessages_SkipFromToSubjectAndAttachment()
    {
        var msg1 = new Dictionary<string, object>
        {
            ["id"] = "1",
            ["from"] = new Dictionary<string, object>
            {
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

    [Fact]
    public async Task GetMailMessageInfosAsync_ReturnsTypedObjects()
    {
        const string json = "{\"value\":[{\"id\":\"1\",\"subject\":\"Hi\"}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var clientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var originalClient = (HttpClient)clientField.GetValue(null)!;
        clientField.SetValue(null, new HttpClient(handler));

        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        var key = "id|tenant||sec|https://graph.microsoft.com";
        cache[key] = new GraphAuthorization { AccessToken = "tok", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5) };

        try
        {
            var cred = new GraphCredential { ClientId = "id", ClientSecret = "sec", DirectoryId = "tenant" };
            var result = await MicrosoftGraphUtils.GetMailMessageInfosAsync(cred, "user@tenant");
            Assert.Single(result);
            Assert.Equal("1", result[0].Id);
            Assert.Equal("user@tenant", result[0].UserPrincipalName);
        }
        finally
        {
            clientField.SetValue(null, originalClient);
            cache.TryRemove(key, out _);
        }
    }

    [Fact]
    public async Task GetMailFolderInfosAsync_ReturnsTypedObjects()
    {
        const string json = "{\"value\":[{\"id\":\"fid\",\"displayName\":\"Inbox\",\"parentFolderId\":\"root\",\"childFolderCount\":0}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var clientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var originalClient = (HttpClient)clientField.GetValue(null)!;
        clientField.SetValue(null, new HttpClient(handler));

        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        var key = "id|tenant||sec|https://graph.microsoft.com";
        cache[key] = new GraphAuthorization { AccessToken = "tok", TokenType = "Bearer", ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5) };

        try
        {
            var cred = new GraphCredential { ClientId = "id", ClientSecret = "sec", DirectoryId = "tenant" };
            var result = await MicrosoftGraphUtils.GetMailFolderInfosAsync(cred, "user@tenant");
            Assert.Single(result);
            Assert.Equal("fid", result[0].Id);
            Assert.Equal("Inbox", result[0].DisplayName);
        }
        finally
        {
            clientField.SetValue(null, originalClient);
            cache.TryRemove(key, out _);
        }
    }
}
