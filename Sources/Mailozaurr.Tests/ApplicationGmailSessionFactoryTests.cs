using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationGmailSessionFactoryTests {
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