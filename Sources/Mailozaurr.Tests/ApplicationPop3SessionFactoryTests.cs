using MailKit.Net.Pop3;
using MailKit.Security;
using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationPop3SessionFactoryTests {
    [Fact]
    public async Task FactoryBuildsBasicAuthSessionRequestFromProfileAndSecrets() {
        var secretStore = new InMemoryMailSecretStore();
        await secretStore.SetSecretAsync("work-pop3", MailSecretNames.Password, "super-secret");

        Pop3SessionRequest? captured = null;
        var factory = new Pop3SessionFactory(secretStore, (request, _) => {
            captured = request;
            return Task.FromResult(new Pop3Client());
        });

        await factory.ConnectAsync(new MailProfile {
            Id = "work-pop3",
            DisplayName = "Work POP3",
            Kind = MailProfileKind.Pop3,
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "pop.example.com",
                [MailProfileSettingsKeys.Port] = "1995",
                [MailProfileSettingsKeys.SecureSocketOptions] = SecureSocketOptions.SslOnConnect.ToString()
            }
        });

        Assert.NotNull(captured);
        Assert.Equal("pop.example.com", captured!.Connection.Server);
        Assert.Equal(1995, captured.Connection.Port);
        Assert.Equal(SecureSocketOptions.SslOnConnect, captured.Connection.Options);
        Assert.Equal("user@example.com", captured.UserName);
        Assert.Equal("super-secret", captured.Secret);
        Assert.Equal(ProtocolAuthMode.Basic, captured.AuthMode);
    }

    [Fact]
    public async Task FactoryUsesOAuthAccessTokenWhenConfigured() {
        var secretStore = new InMemoryMailSecretStore();
        await secretStore.SetSecretAsync("oauth-pop3", MailSecretNames.AccessToken, "oauth-token");

        Pop3SessionRequest? captured = null;
        var factory = new Pop3SessionFactory(secretStore, (request, _) => {
            captured = request;
            return Task.FromResult(new Pop3Client());
        });

        await factory.ConnectAsync(new MailProfile {
            Id = "oauth-pop3",
            DisplayName = "OAuth POP3",
            Kind = MailProfileKind.Pop3,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "pop.example.com",
                [MailProfileSettingsKeys.UserName] = "user@example.com",
                [MailProfileSettingsKeys.AuthMode] = "oauth2"
            }
        });

        Assert.NotNull(captured);
        Assert.Equal(ProtocolAuthMode.OAuth2, captured!.AuthMode);
        Assert.Equal("oauth-token", captured.Secret);
    }
}
