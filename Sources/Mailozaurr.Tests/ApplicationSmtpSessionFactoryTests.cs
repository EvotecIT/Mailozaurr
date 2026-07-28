using MailKit.Security;
using Mailozaurr;
using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationSmtpSessionFactoryTests {
    [Fact]
    public async Task FactoryBuildsBasicAuthSessionRequestFromProfileAndSecrets() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("work-smtp", MailSecretNames.Password, "super-secret");

        SmtpSessionRequest? captured = null;
        var factory = new SmtpSessionFactory(secretStore, (request, _) => {
            captured = request;
            return Task.FromResult(new Smtp());
        });

        await factory.ConnectAsync(new MailProfile {
            Id = "work-smtp",
            DisplayName = "Work SMTP",
            Kind = MailProfileKind.Smtp,
            DefaultSender = "sender@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com",
                [MailProfileSettingsKeys.Port] = "1587",
                [MailProfileSettingsKeys.SecureSocketOptions] = SecureSocketOptions.StartTls.ToString(),
                [MailProfileSettingsKeys.UseSsl] = "true",
                [MailProfileSettingsKeys.MaxDelayMilliseconds] = "9000",
                [MailProfileSettingsKeys.JitterMilliseconds] = "175"
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("smtp.example.com", captured!.Server);
        Assert.Equal(1587, captured.Port);
        Assert.Equal(SecureSocketOptions.StartTls, captured.SecureSocketOptions);
        Assert.True(captured.UseSsl);
        Assert.Equal(9000, captured.MaxDelayMilliseconds);
        Assert.Equal(175, captured.JitterMilliseconds);
        Assert.Equal("sender@example.com", captured.UserName);
        Assert.Equal("super-secret", captured.Password);
        Assert.Equal(ProtocolAuthMode.Basic, captured.AuthMode);
    }

    [Fact]
    public async Task FactoryUsesOAuthAccessTokenWhenConfigured() {
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("gmail-smtp", MailSecretNames.AccessToken, "oauth-token");

        SmtpSessionRequest? captured = null;
        var factory = new SmtpSessionFactory(secretStore, (request, _) => {
            captured = request;
            return Task.FromResult(new Smtp());
        });

        await factory.ConnectAsync(new MailProfile {
            Id = "gmail-smtp",
            DisplayName = "Gmail SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.gmail.com",
                [MailProfileSettingsKeys.UserName] = "user@gmail.com",
                [MailProfileSettingsKeys.AuthMode] = "oauth2"
            }
        });

        Assert.NotNull(captured);
        Assert.Equal(ProtocolAuthMode.OAuth2, captured!.AuthMode);
        Assert.Equal("oauth-token", captured.Password);
        Assert.Equal("user@gmail.com", captured.UserName);
    }

    [Fact]
    public async Task FactorySupportsAnonymousSmtpWithoutCredentials() {
        SmtpSessionRequest? captured = null;
        var factory = new SmtpSessionFactory(null, (request, _) => {
            captured = request;
            return Task.FromResult(new Smtp());
        });

        await factory.ConnectAsync(new MailProfile {
            Id = "anonymous-relay",
            DisplayName = "Anonymous relay",
            Kind = MailProfileKind.Smtp,
            DefaultSender = "sender@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "relay.example.com",
                [MailProfileSettingsKeys.AuthenticationEnabled] = "false"
            }
        });

        Assert.NotNull(captured);
        Assert.False(captured!.Authenticate);
        Assert.Equal(string.Empty, captured.UserName);
        Assert.Equal(string.Empty, captured.Password);
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
