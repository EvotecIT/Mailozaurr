using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace Mailozaurr.Tests;

/// <summary>Regression coverage for the public JMAP protocol, profile, and evidence contracts.</summary>
public sealed class JmapContractTests {
    [Fact]
    public void CallerOwnedHttpClient_RequiresRedirectDisabledAcknowledgement() {
        using var httpClient = new HttpClient(new RecordingHandler());

        var exception = Assert.Throws<ArgumentException>(() => new JmapApiClient(
            new Uri("https://mail.example.test/.well-known/jmap"),
            "secret-token",
            httpClient));

        Assert.Equal("httpClient", exception.ParamName);
        Assert.Contains("disable automatic redirects", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionDiscovery_FollowsSameOriginRedirectAndPreservesUploadUrl() {
        var redirect = new HttpResponseMessage(HttpStatusCode.Redirect) {
            Headers = { Location = new Uri("/jmap/session", UriKind.Relative) }
        };
        var handler = new RecordingHandler(
            redirect,
            JsonResponse(SessionJson("https://mail.example.test/jmap/api", uploadUrl: "https://mail.example.test/upload/{accountId}")));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        var session = await client.GetSessionAsync();

        Assert.Equal("https://mail.example.test/upload/{accountId}", session.UploadUrl);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
        });
        Assert.Equal("https://mail.example.test/jmap/session", handler.Requests[1].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task SessionDiscovery_RequiresExplicitAuthorizationForCrossOriginRedirect() {
        var redirect = new HttpResponseMessage(HttpStatusCode.Redirect) {
            Headers = { Location = new Uri("https://api.example.test/jmap/session") }
        };
        var handler = new RecordingHandler(redirect);
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "secret-token", httpClient, callerOwnedClientDisablesRedirects: true);

        var exception = await Assert.ThrowsAsync<JmapApiException>(() => client.GetSessionAsync());

        Assert.Equal("crossOriginRedirect", exception.ErrorType);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SessionDiscovery_ReappliesBearerTokenToExplicitlyAuthorizedCrossOriginRedirect() {
        var redirect = new HttpResponseMessage(HttpStatusCode.Redirect) {
            Headers = { Location = new Uri("https://api.example.test/jmap/session") }
        };
        var handler = new RecordingHandler(
            redirect,
            JsonResponse(SessionJson("https://api.example.test/jmap/api")));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(
            new Uri("https://mail.example.test/.well-known/jmap"),
            "secret-token",
            httpClient,
            allowCrossOriginApiUrl: true,
            callerOwnedClientDisablesRedirects: true);

        var session = await client.GetSessionAsync();

        Assert.Equal("https://api.example.test/jmap/api", session.ApiUrl);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("api.example.test", handler.Requests[1].RequestUri?.Host);
        Assert.Equal("secret-token", handler.Requests[1].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ProfileValidationFactoryAndAuthStatusShareJmapSecurityRules() {
        var profile = new MailProfile {
            Id = "jmap-work",
            DisplayName = "Work JMAP",
            Kind = MailProfileKind.Jmap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://mail.example.test/.well-known/jmap",
                [MailProfileSettingsKeys.JmapAccountId] = " account ",
                [MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl] = "true"
            }
        };
        var validation = MailProfileValidator.Validate(profile);
        Assert.True(validation.Succeeded);
        Assert.Empty(validation.Warnings);

        var invalid = new MailProfile {
            Id = "jmap-invalid",
            DisplayName = "Invalid JMAP",
            Kind = MailProfileKind.Jmap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://user@mail.example.test/jmap#fragment"
            }
        };
        Assert.False(MailProfileValidator.Validate(invalid).Succeeded);

        var profiles = new InMemoryMailProfileStore();
        var secrets = new InMemoryMailSecretStore();
        await profiles.SaveAsync(profile);
        await secrets.SetSecretAsync(profile.Id, MailSecretNames.AccessToken, "token");
        JmapSessionRequest? captured = null;
        var factory = new JmapSessionFactory(secrets, (request, _) => {
            captured = request;
            return Task.FromResult(new JmapSession(new JmapApiClient(request.SessionUrl, request.AccessToken), request.AccountId));
        });

        using (await factory.ConnectAsync(profile)) { }
        Assert.NotNull(captured);
        Assert.True(captured!.AllowCrossOriginApiUrl);
        Assert.Equal(" account ", captured.AccountId);

        var application = new MailApplicationBuilder()
            .UseProfileStore(profiles)
            .UseSecretStore(secrets)
            .Build();
        var auth = await application.ProfileAuth.GetStatusAsync(profile.Id);
        Assert.Equal("manualToken", auth?.Mode);
    }

    [Fact]
    public async Task QueryEmails_PreservesZeroLimitAndSerializesUtcDatesAndAscendingComparator() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse("Email/query", "{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":0,\"ids\":[],\"total\":0}", "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        await client.QueryEmailsAsync(
            new JmapEmailFilter {
                Before = new DateTimeOffset(2026, 8, 22, 20, 0, 0, TimeSpan.FromHours(2)),
                After = new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.FromHours(2))
            },
            new[] { new JmapComparator() },
            limit: 0);

        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"limit\":0", body, StringComparison.Ordinal);
        Assert.Contains("\"before\":\"2026-08-22T18:00:00Z\"", body, StringComparison.Ordinal);
        Assert.Contains("\"after\":\"2026-08-21T18:00:00Z\"", body, StringComparison.Ordinal);
        Assert.Contains("\"isAscending\":true", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetEmails_PreservesOpaqueIdsEmptyProjectionAndUnmodelledProperties() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse(
                "Email/get",
                "{\"accountId\":\"a1\",\"state\":\"e1\",\"list\":[{\"id\":\" id \",\"to\":null,\"bodyStructure\":{\"type\":\"text/plain\"}}],\"notFound\":null}",
                "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var result = await client.GetEmailsAsync(new[] { " id ", "id" }, Array.Empty<string>());

        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"ids\":[\" id \",\"id\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"properties\":[]", body, StringComparison.Ordinal);
        var email = Assert.Single(result.List);
        Assert.Empty(email.To);
        Assert.Empty(result.NotFound);
        Assert.NotNull(email.AdditionalProperties);
        Assert.True(email.AdditionalProperties!.ContainsKey("bodyStructure"));
    }

    [Fact]
    public async Task GetEmails_ExplicitEmptyIdsReturnsCollectionStateWithoutObjects() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse("Email/get", "{\"accountId\":\"a1\",\"state\":\"e1\",\"list\":[],\"notFound\":[]}", "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var result = await client.GetEmailsAsync(Array.Empty<string>());

        Assert.Equal("e1", result.State);
        Assert.Empty(result.List);
        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"ids\":[]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailChanges_PreservesOpaqueStateTokenVerbatim() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse("Email/changes", "{\"accountId\":\"a1\",\"oldState\":\" state \",\"newState\":\"next\",\"hasMoreChanges\":false,\"created\":[],\"updated\":[],\"destroyed\":[]}", "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        await client.GetEmailChangesAsync(" state ");

        var body = await handler.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("\"sinceState\":\" state \"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListMailboxes_PagesIdsBeforeBoundedGetsAndPreservesSubscriptionState() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse("Mailbox/query", "{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":0,\"ids\":[\"m1\",\"m2\"],\"total\":3}", "c1"),
            MethodResponse("Mailbox/get", "{\"accountId\":\"a1\",\"state\":\"m1\",\"list\":[{\"id\":\"m1\",\"name\":\"Inbox\",\"isSubscribed\":true},{\"id\":\"m2\",\"name\":\"Archive\",\"isSubscribed\":false}]}", "c2"),
            MethodResponse("Mailbox/query", "{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":2,\"ids\":[\"m3\"],\"total\":3}", "c3"),
            MethodResponse("Mailbox/get", "{\"accountId\":\"a1\",\"state\":\"m1\",\"list\":[{\"id\":\"m3\",\"name\":\"Shared\"}]}", "c4"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var mailboxes = await client.ListMailboxesAsync();

        Assert.Equal(3, mailboxes.Count);
        Assert.False(mailboxes[1].IsSubscribed);
        Assert.Null(mailboxes[2].IsSubscribed);
        Assert.Equal(5, handler.Requests.Count);
        var secondQuery = await handler.Requests[3].Content!.ReadAsStringAsync();
        Assert.Contains("\"position\":2", secondQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListMailboxes_FailsWhenQueryStateChangesBetweenPages() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse("Mailbox/query", "{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":0,\"ids\":[\"m1\",\"m2\"],\"total\":3}", "c1"),
            MethodResponse("Mailbox/get", "{\"accountId\":\"a1\",\"state\":\"m1\",\"list\":[{\"id\":\"m1\"},{\"id\":\"m2\"}],\"notFound\":[]}", "c2"),
            MethodResponse("Mailbox/query", "{\"accountId\":\"a1\",\"queryState\":\"q2\",\"position\":2,\"ids\":[\"m3\"],\"total\":3}", "c3"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var exception = await Assert.ThrowsAsync<JmapApiException>(() => client.ListMailboxesAsync());

        Assert.Equal("stateChanged", exception.ErrorType);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task GetThreads_FailsWhenAnyRequestedIdentifierIsMissing() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse("Thread/get", "{\"accountId\":\"a1\",\"state\":\"t1\",\"list\":[{\"id\":\"t1\",\"emailIds\":null}],\"notFound\":[\"missing\"]}", "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var exception = await Assert.ThrowsAsync<JmapApiException>(() => client.GetThreadsAsync(new[] { "t1", "missing" }));

        Assert.Equal("notFound", exception.ErrorType);
    }

    [Fact]
    public async Task ListIdentities_PreservesStandardReplyAndSignatureFields() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api", includeSubmission: true)),
            MethodResponse("Identity/get", "{\"accountId\":\"a1\",\"state\":\"i1\",\"list\":[{\"id\":\"i1\",\"name\":\"User\",\"email\":\"user@example.test\",\"replyTo\":[{\"email\":\"reply@example.test\"}],\"bcc\":null,\"textSignature\":\"Thanks\",\"htmlSignature\":\"<b>Thanks</b>\"}]}", "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var identity = Assert.Single(await client.ListIdentitiesAsync());

        Assert.Equal("reply@example.test", Assert.Single(identity.ReplyTo).Email);
        Assert.Empty(identity.Bcc);
        Assert.Equal("Thanks", identity.TextSignature);
        Assert.Equal("<b>Thanks</b>", identity.HtmlSignature);
    }

    [Fact]
    public async Task ListIdentities_RejectsReplyBeyondAdvertisedGetBound() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api", includeSubmission: true)),
            MethodResponse(
                "Identity/get",
                "{\"accountId\":\"a1\",\"state\":\"i1\",\"list\":[{\"id\":\"i1\"},{\"id\":\"i2\"},{\"id\":\"i3\"}],\"notFound\":[]}",
                "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var exception = await Assert.ThrowsAsync<JmapApiException>(() => client.ListIdentitiesAsync());

        Assert.Equal("invalidResponse", exception.ErrorType);
        Assert.Contains("maxObjectsInGet", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionCache_IsNotMutableThroughReturnedResource() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api")),
            MethodResponse("Email/query", "{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":0,\"ids\":[],\"total\":0}", "c1"));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        var returned = await client.GetSessionAsync();
        returned.ApiUrl = "https://attacker.example/jmap/api";
        returned.Accounts.Clear();
        returned.Capabilities.Clear();

        await client.QueryEmailsAsync(limit: 0);
        var second = await client.GetSessionAsync();

        Assert.Equal("https://mail.example.test/jmap/api", second.ApiUrl);
        Assert.True(second.Accounts.ContainsKey("a1"));
        Assert.True(second.Capabilities.ContainsKey(JmapCapabilities.Core));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("mail.example.test", handler.Requests[1].RequestUri?.Host);
    }

    [Fact]
    public async Task MethodSessionStateChange_InvalidatesCachedSession() {
        var handler = new RecordingHandler(
            JsonResponse(SessionJson("https://mail.example.test/jmap/api", state: "s1")),
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"sessionState\":\"s2\",\"methodResponses\":[[\"Email/query\",{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":0,\"ids\":[],\"total\":0},\"c1\"]]}")
            },
            JsonResponse(SessionJson("https://mail.example.test/jmap/api", state: "s2")));
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        await client.QueryEmailsAsync(limit: 0);
        var refreshed = await client.GetSessionAsync();

        Assert.Equal("s2", refreshed.State);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ConcurrentMethodCalls_AreSerializedBelowEveryAdvertisedPositiveLimit() {
        var handler = new ConcurrentJmapHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new JmapApiClient(new Uri("https://mail.example.test/.well-known/jmap"), "token", httpClient, callerOwnedClientDisablesRedirects: true);

        await Task.WhenAll(
            client.QueryEmailsAsync(limit: 0),
            client.QueryEmailsAsync(limit: 0));

        Assert.Equal(1, handler.MaximumConcurrentMethodRequests);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) {
        Content = new StringContent(json)
    };

    private static HttpResponseMessage MethodResponse(string method, string result, string callId) =>
        JsonResponse($"{{\"methodResponses\":[[\"{method}\",{result},\"{callId}\"]]}}");

    private static string SessionJson(
        string apiUrl,
        string state = "s1",
        string? uploadUrl = null,
        bool includeSubmission = false) {
        var submission = includeSubmission ? ",\"" + JmapCapabilities.Submission + "\":{}" : string.Empty;
        var submissionPrimary = includeSubmission ? ",\"" + JmapCapabilities.Submission + "\":\"a1\"" : string.Empty;
        var upload = uploadUrl == null ? string.Empty : ",\"uploadUrl\":" + JsonSerializer.Serialize(uploadUrl);
        return "{\"capabilities\":{\"" + JmapCapabilities.Core + "\":{\"maxObjectsInGet\":2,\"maxConcurrentRequests\":1},\"" + JmapCapabilities.Mail + "\":{}" + submission + "}," +
               "\"accounts\":{\"a1\":{\"name\":\"Primary\",\"isPersonal\":true,\"isReadOnly\":false,\"accountCapabilities\":{\"" + JmapCapabilities.Mail + "\":{}" + submission + "}}}," +
               "\"primaryAccounts\":{\"" + JmapCapabilities.Mail + "\":\"a1\"" + submissionPrimary + "}," +
               "\"username\":\"user@example.test\",\"apiUrl\":" + JsonSerializer.Serialize(apiUrl) + upload + ",\"state\":" + JsonSerializer.Serialize(state) + "}";
    }

    private sealed class ConcurrentJmapHandler : HttpMessageHandler {
        private int _activeMethodRequests;
        private int _maximumConcurrentMethodRequests;

        internal int MaximumConcurrentMethodRequests => _maximumConcurrentMethodRequests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.Method == HttpMethod.Get) return JsonResponse(SessionJson("https://mail.example.test/jmap/api"));
            var active = Interlocked.Increment(ref _activeMethodRequests);
            UpdateMaximum(active);
            try {
                await Task.Delay(50, cancellationToken);
                var body = await request.Content!.ReadAsStringAsync();
                using var document = JsonDocument.Parse(body);
                var call = document.RootElement.GetProperty("methodCalls")[0];
                var method = call[0].GetString()!;
                var callId = call[2].GetString()!;
                return MethodResponse(method, "{\"accountId\":\"a1\",\"queryState\":\"q1\",\"position\":0,\"ids\":[],\"total\":0}", callId);
            } finally {
                Interlocked.Decrement(ref _activeMethodRequests);
            }
        }

        private void UpdateMaximum(int active) {
            while (true) {
                var observed = _maximumConcurrentMethodRequests;
                if (active <= observed || Interlocked.CompareExchange(ref _maximumConcurrentMethodRequests, active, observed) == observed) return;
            }
        }
    }
}
