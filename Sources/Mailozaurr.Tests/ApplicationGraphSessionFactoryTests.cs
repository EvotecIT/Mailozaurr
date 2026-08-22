using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationGraphSessionFactoryTests {
    [Fact]
    public void GraphSessionPreservesOriginalConstructorSignature() {
        var constructor = typeof(GraphSession).GetConstructor(new[] {
            typeof(GraphApiClient),
            typeof(string),
            typeof(OAuthCredential),
            typeof(GraphCredential)
        });

        Assert.NotNull(constructor);
    }

    [Fact]
    public async Task FactoryUsesAccessTokenSecretWhenAvailable() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("work-graph", MailSecretNames.AccessToken, "graph-token");

        GraphSessionRequest? captured = null;
        var factory = new GraphSessionFactory(
            secretStore,
            connectAsync: (request, cancellationToken) => {
                captured = request;
                return Task.FromResult(new GraphSession(new GraphApiClient(request.Credential), request.UserId));
            });

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "work-graph",
            DisplayName = "Work Graph",
            Kind = MailProfileKind.Graph,
            DefaultMailbox = "user@example.com"
        });

        Assert.NotNull(captured);
        Assert.Equal("user@example.com", captured!.UserId);
        Assert.Equal("graph-token", captured.Credential.AccessToken);
        Assert.Equal("user@example.com", captured.Credential.UserName);
        Assert.Equal(GraphSessionAuthenticationMode.Unknown, captured.AuthenticationMode);
    }

    [Fact]
    public async Task FactoryBuildsClientCredentialRequestWhenAccessTokenIsMissing() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("tenant-graph", MailSecretNames.ClientSecret, "top-secret");

        GraphCredential? captured = null;
        GraphSessionRequest? capturedRequest = null;
        var factory = new GraphSessionFactory(
            secretStore,
            acquireCredentialAsync: (profile, credential, cancellationToken) => {
                captured = credential;
                return Task.FromResult(new OAuthCredential {
                    AccessToken = "issued-token",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                });
            },
            connectAsync: (request, cancellationToken) => {
                capturedRequest = request;
                return Task.FromResult(new GraphSession(new GraphApiClient(request.Credential), request.UserId));
            });

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "tenant-graph",
            DisplayName = "Tenant Graph",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.TenantId] = "tenant-id",
                [MailProfileSettingsKeys.Mailbox] = "shared@example.com"
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("client-id", captured!.ClientId);
        Assert.Equal("tenant-id", captured.DirectoryId);
        Assert.Equal("top-secret", captured.ClientSecret);
        Assert.Equal("shared@example.com", session.UserId);
        Assert.Equal(GraphSessionAuthenticationMode.Application, capturedRequest?.AuthenticationMode);
    }

    [Fact]
    public async Task FactoryDoesNotClassifyStoredTokenFromAvailableClientCredentialMetadata() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("hybrid-graph", MailSecretNames.AccessToken, "opaque-token");
        await secretStore.SetSecretAsync("hybrid-graph", MailSecretNames.ClientSecret, "top-secret");
        GraphSessionRequest? captured = null;
        var factory = new GraphSessionFactory(
            secretStore,
            connectAsync: (request, cancellationToken) => {
                captured = request;
                return Task.FromResult(new GraphSession(new GraphApiClient(request.Credential), request.UserId));
            });

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "hybrid-graph",
            DisplayName = "Hybrid Graph",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.TenantId] = "tenant-id"
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("opaque-token", captured!.Credential.AccessToken);
        Assert.NotNull(captured.GraphCredential);
        Assert.Equal(GraphSessionAuthenticationMode.Unknown, captured.AuthenticationMode);
    }

    [Fact]
    public async Task FactoryUsesSilentInteractiveRefreshWhenStoredTokenIsExpired() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("work-graph", MailSecretNames.AccessToken, "expired-token");
        GraphSessionRequest? captured = null;

        var factory = new GraphSessionFactory(
            secretStore,
            acquireSilentCredentialAsync: (profile, cancellationToken) =>
                Task.FromResult<OAuthCredential?>(new OAuthCredential {
                    UserName = "user@example.com",
                    AccessToken = "silent-token",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                }),
            connectAsync: (request, cancellationToken) => {
                captured = request;
                return Task.FromResult(new GraphSession(new GraphApiClient(request.Credential), request.UserId, request.Credential, request.GraphCredential));
            });

        using var session = await factory.ConnectAsync(new MailProfile {
            Id = "work-graph",
            DisplayName = "Work Graph",
            Kind = MailProfileKind.Graph,
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.AuthFlow] = MailProfileAuthFlowNames.Interactive,
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.TenantId] = "tenant-id",
                [MailProfileSettingsKeys.RedirectUri] = MailProfileAuthDefaults.GraphRedirectUri,
                [MailProfileSettingsKeys.TokenExpiresOn] = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o", System.Globalization.CultureInfo.InvariantCulture)
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("silent-token", captured!.Credential.AccessToken);
        Assert.Equal("user@example.com", session.UserId);
        Assert.Equal(GraphSessionAuthenticationMode.Delegated, captured.AuthenticationMode);
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
