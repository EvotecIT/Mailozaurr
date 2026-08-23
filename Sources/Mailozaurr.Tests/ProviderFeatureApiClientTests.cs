using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace Mailozaurr.Tests;

public class ProviderFeatureApiClientTests {
    [Fact]
    public async Task GraphRules_ListFollowsOnlySameOriginContinuation() {
        var first = "{\"value\":[{\"id\":\"r1\",\"displayName\":\"One\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messageRules?$skiptoken=next\"}";
        var second = "{\"value\":[{\"id\":\"r2\",\"displayName\":\"Two\"}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(first) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(second) });
        using var client = CreateGraphClient(handler);

        var rules = await client.ListInboxRulesAsync("me", top: 25);

        Assert.Equal(new[] { "r1", "r2" }, rules.Select(rule => rule.Id));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("$top=25", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task GraphRules_RejectsCrossOriginContinuation() {
        var body = "{\"value\":[],\"@odata.nextLink\":\"https://attacker.example/steal\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGraphClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.ListInboxRulesAsync());

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GraphRules_RejectsSameOriginContinuationForAnotherResource() {
        var body = "{\"value\":[],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/me/messages?$skiptoken=next\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGraphClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.ListInboxRulesAsync());

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GraphRules_ThrowsWhenProviderContinuationExceedsPageBound() {
        var body = "{\"value\":[{\"id\":\"r1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messageRules?$skiptoken=next\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGraphClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.ListInboxRulesAsync(maxPages: 1));

        Assert.Contains("page bound", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GraphRules_ResultLimitStopsBeforeFollowingContinuation() {
        var body = "{\"value\":[{\"id\":\"r1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messageRules?$skiptoken=next\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGraphClient(handler);

        var rules = await client.ListInboxRulesAsync(top: 1, maxPages: 25);

        Assert.Equal("r1", Assert.Single(rules).Id);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GraphConversation_RejectsCrossOriginContinuationBeforeSendingToken() {
        var body = "{\"value\":[],\"@odata.nextLink\":\"https://attacker.example/steal\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGraphClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.ListConversationMessagesAsync("conversation-1"));

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GraphConversation_TopRemainsProviderPageSizeAcrossContinuation() {
        var first = "{\"value\":[{\"id\":\"m1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/me/messages?$skiptoken=next\"}";
        var second = "{\"value\":[{\"id\":\"m2\"}]}";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(first) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(second) });
        using var client = CreateGraphClient(handler);

        var messages = await client.ListConversationMessagesAsync("conversation-1", top: 1);

        Assert.Equal(new[] { "m1", "m2" }, messages.Select(message => message.Id));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GraphEvents_CreateEscapesMailboxAndUsesTypedPayload() {
        var body = "{\"id\":\"e1\",\"subject\":\"Review\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(body) });
        using var client = CreateGraphClient(handler);

        var created = await client.CreateEventAsync(new GraphEvent { Subject = "Review" }, "user+tag@example.test");

        Assert.Equal("e1", created.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("users/user%2Btag%40example.test/events", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphWrites_OmitReadOnlyFieldsAndUseGraphAttendeeShape() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"r2\"}") },
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"e2\"}") });
        using var client = CreateGraphClient(handler);

        await client.CreateInboxRuleAsync(new GraphInboxRule {
            Id = "server-rule",
            DisplayName = "Rule",
            IsReadOnly = true,
            HasError = true
        });
        await client.CreateEventAsync(new GraphEvent {
            Id = "server-event",
            Subject = "Review",
            Attendees = new List<GraphEventAttendee> {
                new() { EmailAddress = new GraphEmail { Address = "user@example.test", Name = "User" }, Type = "required" }
            }
        });

        using var rulePayload = JsonDocument.Parse(await handler.Requests[0].Content!.ReadAsStringAsync());
        Assert.Equal("Rule", rulePayload.RootElement.GetProperty("displayName").GetString());
        Assert.False(rulePayload.RootElement.TryGetProperty("id", out _));
        Assert.False(rulePayload.RootElement.TryGetProperty("isReadOnly", out _));
        Assert.False(rulePayload.RootElement.TryGetProperty("hasError", out _));

        using var eventPayload = JsonDocument.Parse(await handler.Requests[1].Content!.ReadAsStringAsync());
        Assert.False(eventPayload.RootElement.TryGetProperty("id", out _));
        var emailAddress = eventPayload.RootElement.GetProperty("attendees")[0].GetProperty("emailAddress");
        Assert.Equal("user@example.test", emailAddress.GetProperty("address").GetString());
        Assert.False(emailAddress.TryGetProperty("emailAddress", out _));
    }

    [Fact]
    public async Task GraphEvents_ListUsesDefaultBoundedProjection() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"value\":[]}")
        });
        using var client = CreateGraphClient(handler);

        _ = await client.ListEventsAsync();

        var query = Assert.Single(handler.Requests).RequestUri!.Query;
        Assert.Contains("$select=id%2Csubject%2Cstart%2Cend%2Cbody%2Cattendees", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GmailFilters_ListEscapesMailbox() {
        var body = "{\"filter\":[{\"id\":\"f1\",\"criteria\":{\"from\":\"sender@example.test\"},\"action\":{\"addLabelIds\":[\"STARRED\"]}}]}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGmailClient(handler);

        var filters = await client.ListFiltersAsync("user+tag@example.test");

        Assert.Equal("f1", Assert.Single(filters).Id);
        Assert.Contains("users/user%2Btag%40example.test/settings/filters", Assert.Single(handler.Requests).RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GmailLabels_UpdateUsesPatchAndEscapedId() {
        var body = "{\"id\":\"Label 1\",\"name\":\"Archive\",\"type\":\"user\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGmailClient(handler);

        var label = await client.UpdateLabelAsync("me", "Label 1", new GmailLabel { Name = "Archive" });

        Assert.Equal("Archive", label.Name);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("PATCH", request.Method.Method);
        Assert.EndsWith("/users/me/labels/Label%201", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GmailWrites_UseApiNamesAndOmitServerManagedFields() {
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"f2\"}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"Label_2\",\"name\":\"Archive\"}") });
        using var client = CreateGmailClient(handler);

        await client.CreateFilterAsync("me", new GmailFilter {
            Id = "server-filter",
            Criteria = new GmailFilterCriteria { From = "sender@example.test" },
            Action = new GmailFilterAction { AddLabelIds = new List<string> { "STARRED" } }
        });
        await client.UpdateLabelAsync("me", "Label_2", new GmailLabel {
            Id = "server-label",
            Name = "Archive",
            Type = "user",
            MessageListVisibility = "show",
            LabelListVisibility = "labelShow",
            MessagesTotal = 10,
            MessagesUnread = 2,
            ThreadsTotal = 5,
            ThreadsUnread = 1,
            Color = new GmailLabelColor { TextColor = "#ffffff", BackgroundColor = "#000000" }
        });

        using var filterPayload = JsonDocument.Parse(await handler.Requests[0].Content!.ReadAsStringAsync());
        Assert.False(filterPayload.RootElement.TryGetProperty("id", out _));
        Assert.Equal("sender@example.test", filterPayload.RootElement.GetProperty("criteria").GetProperty("from").GetString());

        using var labelPayload = JsonDocument.Parse(await handler.Requests[1].Content!.ReadAsStringAsync());
        Assert.Equal("Archive", labelPayload.RootElement.GetProperty("name").GetString());
        Assert.Equal("show", labelPayload.RootElement.GetProperty("messageListVisibility").GetString());
        Assert.Equal("#ffffff", labelPayload.RootElement.GetProperty("color").GetProperty("textColor").GetString());
        foreach (var readOnlyName in new[] { "id", "type", "messagesTotal", "messagesUnread", "threadsTotal", "threadsUnread" }) {
            Assert.False(labelPayload.RootElement.TryGetProperty(readOnlyName, out _));
        }
    }

    [Fact]
    public async Task GmailSettingsMutations_HonorDryRunWithoutSendingRequests() {
        var handler = new RecordingHandler();
        using var client = CreateGmailClient(handler);
        client.DryRun = true;

        var filter = new GmailFilter { Criteria = new GmailFilterCriteria { From = "sender@example.test" } };
        var label = new GmailLabel { Name = "Archive" };
        Assert.Same(filter, await client.CreateFilterAsync("me", filter));
        await client.DeleteFilterAsync("me", "filter-1");
        Assert.Same(label, await client.CreateLabelAsync("me", label));
        Assert.Same(label, await client.UpdateLabelAsync("me", "Label_1", label));
        await client.DeleteLabelAsync("me", "Label_1");

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GmailThreads_ListReturnsOneProviderPage() {
        var body = "{\"threads\":[{\"id\":\"t1\"},{\"id\":\"t2\"}],\"nextPageToken\":\"next\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGmailClient(handler);

        var page = await client.ListThreadsPageAsync("me", query: "is:unread", pageSize: 2);

        Assert.Equal(2, page.Threads.Count);
        Assert.Equal("next", page.NextPageToken);
        Assert.Single(handler.Requests);
        Assert.Contains("q=is%3Aunread", handler.Requests[0].RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GmailProfile_EscapesMailboxOverrideAsOnePathSegment() {
        var body = "{\"emailAddress\":\"user@example.test\",\"messagesTotal\":1,\"threadsTotal\":1,\"historyId\":\"2\"}";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        using var client = CreateGmailClient(handler);

        _ = await client.GetProfileAsync("shared/mailbox?#fragment");

        Assert.Equal(
            "/gmail/v1/users/shared%2Fmailbox%3F%23fragment/profile",
            Assert.Single(handler.Requests).RequestUri!.AbsolutePath);
    }

    private static GraphApiClient CreateGraphClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") }, credential: new OAuthCredential {
            AccessToken = "token",
            ExpiresOn = DateTimeOffset.MaxValue
        });

    private static GmailApiClient CreateGmailClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") }, credential: new OAuthCredential {
            AccessToken = "token",
            ExpiresOn = DateTimeOffset.MaxValue
        });
}
