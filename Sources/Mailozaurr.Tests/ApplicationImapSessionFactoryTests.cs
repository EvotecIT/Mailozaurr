using MailKit.Net.Imap;
using MailKit.Security;
using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationImapSessionFactoryTests {
    [Fact]
    public async Task FactoryBuildsBasicAuthSessionRequestFromProfileAndSecrets() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("work-imap", MailSecretNames.Password, "super-secret");

        ImapSessionRequest? captured = null;
        var factory = new ImapSessionFactory(secretStore, (request, _) => {
            captured = request;
            return Task.FromResult(new ImapClient());
        });

        await factory.ConnectAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com",
                [MailProfileSettingsKeys.Port] = "1993",
                [MailProfileSettingsKeys.SecureSocketOptions] = SecureSocketOptions.SslOnConnect.ToString()
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("imap.example.com", captured!.Connection.Server);
        Assert.Equal(1993, captured.Connection.Port);
        Assert.Equal(SecureSocketOptions.SslOnConnect, captured.Connection.Options);
        Assert.Equal("user@example.com", captured.UserName);
        Assert.Equal("super-secret", captured.Secret);
        Assert.Equal(ProtocolAuthMode.Basic, captured.AuthMode);
    }

    [Fact]
    public async Task FactoryUsesOAuthAccessTokenWhenConfigured() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("gmail-imap", MailSecretNames.AccessToken, "oauth-token");

        ImapSessionRequest? captured = null;
        var factory = new ImapSessionFactory(secretStore, (request, _) => {
            captured = request;
            return Task.FromResult(new ImapClient());
        });

        await factory.ConnectAsync(new MailProfile {
            Id = "gmail-imap",
            DisplayName = "Gmail IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.gmail.com",
                [MailProfileSettingsKeys.UserName] = "user@gmail.com",
                [MailProfileSettingsKeys.AuthMode] = "oauth2"
            }
        });

        Assert.NotNull(captured);
        Assert.Equal(ProtocolAuthMode.OAuth2, captured!.AuthMode);
        Assert.Equal("oauth-token", captured.Secret);
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