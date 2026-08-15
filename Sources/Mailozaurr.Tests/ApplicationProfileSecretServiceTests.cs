using Mailozaurr.Hosting;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileSecretServiceTests {
    [Fact]
    public async Task SetSecretAsyncRequiresExistingProfile() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new FileMailSecretStore(CreateTemporaryFilePath("secrets.json"), new TestCredentialProtector());
        var service = new MailProfileSecretService(profileStore, secretStore);

        var result = await service.SetSecretAsync("missing", MailSecretNames.Password, "secret");

        Assert.False(result.Succeeded);
        Assert.Equal("profile_not_found", result.Code);
    }

    [Fact]
    public async Task SetSecretAsyncPersistsSecretForExistingProfile() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new FileMailSecretStore(CreateTemporaryFilePath("secrets.json"), new TestCredentialProtector());
        var service = new MailProfileSecretService(profileStore, secretStore);

        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });

        var result = await service.SetSecretAsync("work-imap", MailSecretNames.Password, "secret");
        var stored = await secretStore.GetSecretAsync("work-imap", MailSecretNames.Password);

        Assert.True(result.Succeeded);
        Assert.Equal("secret", stored);
    }

    [Fact]
    public async Task SetSecretAsyncSupportsReferenceCopyForExistingProfile() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new FileMailSecretStore(CreateTemporaryFilePath("secrets.json"), new TestCredentialProtector());
        var service = new MailProfileSecretService(profileStore, secretStore);

        await profileStore.SaveAsync(new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com"
            }
        });
        await profileStore.SaveAsync(new MailProfile {
            Id = "shared-secrets",
            DisplayName = "Shared Secrets",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Mailbox] = "shared@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await secretStore.SetSecretAsync("shared-secrets", MailSecretNames.Password, "copied-secret");

        var result = await service.SetSecretAsync("work-imap", MailSecretNames.Password, null, $"shared-secrets:{MailSecretNames.Password}");
        var stored = await secretStore.GetSecretAsync("work-imap", MailSecretNames.Password);

        Assert.True(result.Succeeded);
        Assert.Equal("copied-secret", stored);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class TestCredentialProtector : ICredentialProtector {
        public string Protect(string plainText) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"protected::{plainText}"));

        public string Unprotect(string protectedData) {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedData));
            return decoded.StartsWith("protected::", StringComparison.Ordinal)
                ? decoded.Substring("protected::".Length)
                : decoded;
        }
    }
}