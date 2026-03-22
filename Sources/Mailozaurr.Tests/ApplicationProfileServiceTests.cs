using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileServiceTests {
    [Fact]
    public async Task SaveAsyncReturnsValidationErrorForInvalidProfile() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var service = new MailProfileService(store);

        var result = await service.SaveAsync(new MailProfile());

        Assert.False(result.Succeeded);
        Assert.Equal("profile_invalid", result.Code);
    }

    [Fact]
    public async Task SetDefaultAsyncMarksRequestedProfileAsDefault() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var service = new MailProfileService(store);

        await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "imap.example.com" }
        });
        await service.SaveAsync(new MailProfile {
            Id = "graph",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph,
            DefaultMailbox = "user@example.com"
        });

        var result = await service.SetDefaultAsync("graph");
        var profiles = await service.GetProfilesAsync();

        Assert.True(result.Succeeded);
        Assert.False(profiles.Single(p => p.Id == "imap").IsDefault);
        Assert.True(profiles.Single(p => p.Id == "graph").IsDefault);
    }

    [Fact]
    public async Task DeleteAsyncRemovesKnownSecretsWhenSecretStoreIsProvided() {
        var profilePath = CreateTemporaryFilePath("profiles.json");
        var secretPath = CreateTemporaryFilePath("secrets.json");
        var store = new FileMailProfileStore(profilePath);
        var secretStore = new FileMailSecretStore(secretPath, new TestCredentialProtector());
        var service = new MailProfileService(store, secretStore);

        await service.SaveAsync(new MailProfile {
            Id = "smtp",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> { [MailProfileSettingsKeys.Server] = "smtp.example.com" }
        });
        await secretStore.SetSecretAsync("smtp", MailSecretNames.Password, "secret");

        var result = await service.DeleteAsync("smtp");
        var secret = await secretStore.GetSecretAsync("smtp", MailSecretNames.Password);

        Assert.True(result.Succeeded);
        Assert.Null(secret);
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
