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
    public async Task DeleteAsyncRemovesKnownAndCustomSecretsWhenSecretStoreIsProvided() {
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
        await secretStore.SetSecretAsync("smtp", "custom-api-key", "custom-secret");

        var result = await service.DeleteAsync("smtp");
        var secret = await secretStore.GetSecretAsync("smtp", MailSecretNames.Password);
        var customSecret = await secretStore.GetSecretAsync("smtp", "custom-api-key");

        Assert.True(result.Succeeded);
        Assert.Null(secret);
        Assert.Null(customSecret);
    }

    [Fact]
    public async Task DeleteAsyncRestoresProfileWhenSecretCleanupFails() {
        var store = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new FailingCleanupSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "smtp",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await secretStore.SetSecretAsync("smtp", "custom-api-key", "custom-secret");

        await Assert.ThrowsAsync<IOException>(() => service.DeleteAsync("smtp"));

        Assert.NotNull(await service.GetProfileAsync("smtp"));
        Assert.Equal("custom-secret", await secretStore.GetSecretAsync("smtp", "custom-api-key"));
    }

    [Fact]
    public async Task DeleteAsyncDoesNotDecryptSecretsBeforeRemovingProfile() {
        var profilePath = CreateTemporaryFilePath("profiles.json");
        var secretPath = CreateTemporaryFilePath("secrets.json");
        var profileStore = new FileMailProfileStore(profilePath);
        var secretStore = new FileMailSecretStore(secretPath, new UnreadableCredentialProtector());
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "smtp",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await secretStore.SetSecretAsync("smtp", "broken-secret", "cannot-be-unprotected");

        OperationResult result = await service.DeleteAsync("smtp");

        Assert.True(result.Succeeded);
        Assert.Null(await service.GetProfileAsync("smtp"));
        Assert.Null(await secretStore.GetSecretAsync("smtp", "broken-secret"));
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

    private sealed class UnreadableCredentialProtector : ICredentialProtector {
        public string Protect(string plainText) => $"unreadable::{plainText}";

        public string Unprotect(string protectedData) =>
            throw new InvalidDataException("Simulated unreadable protected value.");
    }

    private sealed class FailingCleanupSecretStore : IMailSecretStore, IMailProfileSecretCleanup {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _secrets.TryGetValue($"{profileId}::{secretName}", out string? value);
            return Task.FromResult<string?>(value);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _secrets[$"{profileId}::{secretName}"] = secretValue;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.Remove($"{profileId}::{secretName}"));

        public Task<IReadOnlyDictionary<string, string>> GetProfileSecretsAsync(
            string profileId,
            CancellationToken cancellationToken = default) {
            string prefix = profileId + "::";
            IReadOnlyDictionary<string, string> result = _secrets
                .Where(secret => secret.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    secret => secret.Key.Substring(prefix.Length),
                    secret => secret.Value,
                    StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task RemoveProfileSecretsAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("Simulated secret cleanup failure."));
    }
}
