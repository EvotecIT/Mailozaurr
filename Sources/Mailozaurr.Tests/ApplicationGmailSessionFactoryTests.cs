using System.Net;
using System.Net.Http;
using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationGmailSessionFactoryTests {
    [Fact]
    public void FactoryPreservesOriginalConstructorForBinaryCompatibility() {
        var constructor = typeof(GmailSessionFactory).GetConstructor(new[] {
            typeof(IMailSecretStore),
            typeof(Func<GmailSessionFactory.GmailRefreshRequest, CancellationToken, Task<OAuthCredential>>),
            typeof(Func<GmailSessionRequest, CancellationToken, Task<GmailSession>>)
        });

        Assert.NotNull(constructor);
    }

    [Fact]
    public async Task FactoryUsesAccessTokenSecretWhenAvailable() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.AccessToken, "gmail-token");

        GmailSessionRequest? captured = null;
        var factory = new GmailSessionFactory(
            secretStore,
            connectAsync: (request, cancellationToken) => {
                captured = request;
                return Task.FromResult(new GmailSession(new GmailApiClient(request.Credential, request.RefreshAccessTokenAsync), request.UserId));
            });

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "personal-gmail",
            DisplayName = "Personal Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultMailbox = "me"
        });

        Assert.NotNull(captured);
        Assert.Equal("me", captured!.UserId);
        Assert.Equal("gmail-token", captured.Credential.AccessToken);
        Assert.Equal("me", captured.Credential.UserName);
    }

    [Fact]
    public async Task FactoryRefreshesAccessTokenWhenOnlyRefreshTokenIsAvailable() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.RefreshToken, "refresh-token");
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.ClientSecret, "client-secret");

        GmailSessionFactory.GmailRefreshRequest? captured = null;
        GmailSessionRequest? connected = null;
        var factory = new GmailSessionFactory(
            secretStore,
            refreshCredentialAsync: (request, cancellationToken) => {
                captured = request;
                return Task.FromResult(new OAuthCredential {
                    UserName = request.UserId,
                    AccessToken = "issued-token",
                    RefreshToken = request.RefreshToken,
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                });
            },
            connectAsync: (request, cancellationToken) => {
                connected = request;
                return Task.FromResult(new GmailSession(new GmailApiClient(request.Credential, request.RefreshAccessTokenAsync), request.UserId));
            });

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "personal-gmail",
            DisplayName = "Personal Gmail",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.Mailbox] = "me"
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("client-id", captured!.ClientId);
        Assert.Equal("client-secret", captured.ClientSecret);
        Assert.Equal("refresh-token", captured.RefreshToken);
        Assert.NotNull(connected);
        Assert.Equal("issued-token", connected!.Credential.AccessToken);
    }

    [Fact]
    public async Task FactoryRefreshesExpiredAccessTokenWhenRefreshTokenIsAvailable() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.AccessToken, "expired-token");
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.RefreshToken, "refresh-token");
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.ClientSecret, "client-secret");

        GmailSessionFactory.GmailRefreshRequest? captured = null;
        GmailSessionRequest? connected = null;
        var factory = new GmailSessionFactory(
            secretStore,
            refreshCredentialAsync: (request, cancellationToken) => {
                captured = request;
                return Task.FromResult(new OAuthCredential {
                    UserName = request.UserId,
                    AccessToken = "renewed-token",
                    RefreshToken = request.RefreshToken,
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                });
            },
            connectAsync: (request, cancellationToken) => {
                connected = request;
                return Task.FromResult(new GmailSession(new GmailApiClient(request.Credential, request.RefreshAccessTokenAsync), request.UserId));
            });

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "personal-gmail",
            DisplayName = "Personal Gmail",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.Mailbox] = "me",
                [MailProfileSettingsKeys.TokenExpiresOn] = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o", System.Globalization.CultureInfo.InvariantCulture)
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("refresh-token", captured!.RefreshToken);
        Assert.NotNull(connected);
        Assert.Equal("renewed-token", connected!.Credential.AccessToken);
    }

    [Fact]
    public async Task FactoryUsesInjectedTransportForGoogleTokenRefresh() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.RefreshToken, "refresh-token");
        await secretStore.SetSecretAsync("personal-gmail", MailSecretNames.ClientSecret, "client-secret");

        HttpMethod? requestMethod = null;
        Uri? requestUri = null;
        string? requestBody = null;
        using var transport = new HttpMessageInvoker(new CallbackHttpMessageHandler(async (request, cancellationToken) => {
            requestMethod = request.Method;
            requestUri = request.RequestUri;
            requestBody = request.Content == null
                ? null
                : await ReadContentAsync(request.Content, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"access_token\":\"injected-token\",\"expires_in\":3600}")
            };
        }));

        GmailSessionRequest? connected = null;
        var factory = new GmailSessionFactory(
            secretStore,
            refreshCredentialAsync: null,
            connectAsync: (request, cancellationToken) => {
                connected = request;
                return Task.FromResult(new GmailSession(
                    new GmailApiClient(request.Credential, request.RefreshAccessTokenAsync),
                    request.UserId));
            },
            googleTokenTransport: transport);

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "personal-gmail",
            DisplayName = "Personal Gmail",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.Mailbox] = "me"
            }
        });

        Assert.Equal(HttpMethod.Post, requestMethod);
        Assert.Equal(new Uri("https://oauth2.googleapis.com/token"), requestUri);
        Assert.Contains("client_id=client-id", requestBody, StringComparison.Ordinal);
        Assert.Contains("client_secret=client-secret", requestBody, StringComparison.Ordinal);
        Assert.Contains("refresh_token=refresh-token", requestBody, StringComparison.Ordinal);
        Assert.NotNull(connected);
        Assert.Equal("injected-token", connected!.Credential.AccessToken);
    }

    private static Task<string> ReadContentAsync(HttpContent content, CancellationToken cancellationToken) {
#if NET5_0_OR_GREATER
        return content.ReadAsStringAsync(cancellationToken);
#else
        return content.ReadAsStringAsync();
#endif
    }

    private sealed class CallbackHttpMessageHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _callback;

        public CallbackHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) {
            _callback = callback;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => _callback(request, cancellationToken);
    }

    private sealed class InMemorySecretStore : IMailSecretStore {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _values.TryGetValue($"{profileId}::{secretName}", out var value);
            return Task.FromResult<string?>(value);
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.Remove($"{profileId}::{secretName}"));

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _values[$"{profileId}::{secretName}"] = secretValue;
            return Task.CompletedTask;
        }
    }
}
