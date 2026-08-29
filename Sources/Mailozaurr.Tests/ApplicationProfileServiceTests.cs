using Mailozaurr;

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

    [Fact]
    public async Task DeleteAsyncPurgesLegacySecretsUsingKnownProfileIdentity() {
        var profilePath = CreateTemporaryFilePath("profiles.json");
        var secretPath = CreateTemporaryFilePath("secrets.json");
        var profileStore = new FileMailProfileStore(profilePath);
        var protector = new TestCredentialProtector();
        string archiveValue = protector.Protect("archive-secret");
        File.WriteAllText(secretPath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"team::archive::password\":\"{archiveValue}\"}}}}");
        var secretStore = new FileMailSecretStore(secretPath, protector);
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "team",
            DisplayName = "Team",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await service.SaveAsync(new MailProfile {
            Id = "team::archive",
            DisplayName = "Team archive",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        OperationResult result = await service.DeleteAsync("team::archive");

        Assert.True(result.Succeeded);
        Assert.Null(await secretStore.GetSecretAsync("team::archive", "password"));
        Assert.NotNull(await service.GetProfileAsync("team"));
    }

    [Theory]
    [InlineData(MailSecretNames.ApiKey)]
    [InlineData(MailSecretNames.AccessKeyId)]
    [InlineData(MailSecretNames.SecretAccessKey)]
    public async Task DeleteAsyncRemovesProviderSecretsFromBasicStores(string secretName) {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Provider",
            Kind = MailProfileKind.SendGrid
        });
        await secretStore.SetSecretAsync("provider", secretName, "secret");

        var result = await service.DeleteAsync("provider");

        Assert.True(result.Succeeded);
        Assert.Null(await secretStore.GetSecretAsync("provider", secretName));
    }

    [Fact]
    public async Task SaveAsyncRejectsProviderKindChangesWithoutReusingSecrets() {
        var profileStore = new InMemoryMailProfileStore();
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(profileStore, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await secretStore.SetSecretAsync("provider", MailSecretNames.Password, "smtp-secret");

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "SendGrid",
            Kind = MailProfileKind.SendGrid
        });

        Assert.False(result.Succeeded);
        Assert.Equal("profile_kind_change_not_allowed", result.Code);
        Assert.Equal(MailProfileKind.Smtp, (await service.GetProfileAsync("provider"))!.Kind);
        Assert.Equal("smtp-secret", await secretStore.GetSecretAsync("provider", MailSecretNames.Password));
    }

    [Theory]
    [InlineData(MailProfileKind.Smtp, MailProfileSettingsKeys.Server, "smtp.example.com", "smtp.attacker.example")]
    [InlineData(MailProfileKind.Imap, MailProfileSettingsKeys.Port, "993", "143")]
    [InlineData(MailProfileKind.Pop3, MailProfileSettingsKeys.SecureSocketOptions, "SslOnConnect", "None")]
    [InlineData(MailProfileKind.Smtp, MailProfileSettingsKeys.UseSsl, "true", "false")]
    [InlineData(MailProfileKind.Pop3, MailProfileSettingsKeys.SkipCertificateRevocation, "false", "true")]
    [InlineData(MailProfileKind.Imap, MailProfileSettingsKeys.SkipCertificateValidation, "false", "true")]
    [InlineData(MailProfileKind.Jmap, MailProfileSettingsKeys.JmapSessionUrl, "https://mail.example.com/.well-known/jmap", "https://mail.attacker.example/.well-known/jmap")]
    [InlineData(MailProfileKind.Jmap, MailProfileSettingsKeys.JmapAllowCrossOriginApiUrl, "false", "true")]
    [InlineData(MailProfileKind.Ses, MailProfileSettingsKeys.Region, "us-east-1", "eu-central-1")]
    public async Task SaveAsyncRejectsCredentialContextChangesWithoutRedirectingSecrets(
        MailProfileKind kind,
        string setting,
        string originalValue,
        string changedValue) {
        var profileStore = new InMemoryMailProfileStore();
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(profileStore, secretStore);
        var originalSettings = CreateValidSettings(kind);
        originalSettings[setting] = originalValue;
        await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = kind.ToString(),
            Kind = kind,
            Settings = originalSettings
        });
        await secretStore.SetSecretAsync("provider", MailSecretNames.Password, "retained-secret");

        var changedSettings = CreateValidSettings(kind);
        changedSettings[setting] = changedValue;
        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Changed",
            Kind = kind,
            Settings = changedSettings
        });

        Assert.False(result.Succeeded);
        Assert.Equal("profile_credential_context_change_not_allowed", result.Code);
        Assert.Equal(originalValue, (await service.GetProfileAsync("provider"))!.Settings[setting]);
        Assert.Equal("retained-secret", await secretStore.GetSecretAsync("provider", MailSecretNames.Password));
    }

    [Fact]
    public async Task SaveAsyncAllowsNonCredentialSettingsAndEquivalentEndpointFormatting() {
        var store = new InMemoryMailProfileStore();
        var service = new MailProfileService(store);
        await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "IMAP.Example.com",
                [MailProfileSettingsKeys.Port] = "0993",
                [MailProfileSettingsKeys.Folder] = "Inbox"
            }
        });

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "imap",
            DisplayName = "Updated IMAP",
            Kind = MailProfileKind.Imap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "imap.example.com",
                [MailProfileSettingsKeys.Port] = "993",
                [MailProfileSettingsKeys.Folder] = "Archive"
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("Archive", (await service.GetProfileAsync("imap"))!.Settings[MailProfileSettingsKeys.Folder]);
    }

    [Fact]
    public async Task SaveAsyncAllowsExplicitSesDefaultRegionWhenItWasPreviouslyOmitted() {
        var store = new InMemoryMailProfileStore();
        var secretStore = new BasicSecretStore();
        var service = new MailProfileService(store, secretStore);
        await service.SaveAsync(new MailProfile {
            Id = "ses",
            DisplayName = "SES",
            Kind = MailProfileKind.Ses
        });
        await secretStore.SetSecretAsync("ses", MailSecretNames.Password, "retained-secret");

        OperationResult result = await service.SaveAsync(new MailProfile {
            Id = "ses",
            DisplayName = "SES explicit default",
            Kind = MailProfileKind.Ses,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.Region] = "us-east-1"
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("us-east-1", (await service.GetProfileAsync("ses"))!.Settings[MailProfileSettingsKeys.Region]);
        Assert.Equal("retained-secret", await secretStore.GetSecretAsync("ses", MailSecretNames.Password));
    }

    [Fact]
    public async Task InMemoryProfileStoreRejectsProviderKindChangesAtomically() =>
        await AssertProfileStoreRejectsProviderKindChangeAsync(new InMemoryMailProfileStore());

    [Fact]
    public async Task FileProfileStoreRejectsProviderKindChangesAtomically() =>
        await AssertProfileStoreRejectsProviderKindChangeAsync(
            new FileMailProfileStore(CreateTemporaryFilePath("profiles.json")));

    [Fact]
    public async Task InMemoryProfileStoreRejectsCredentialContextChangesAtomically() =>
        await AssertProfileStoreRejectsCredentialContextChangeAsync(new InMemoryMailProfileStore());

    [Fact]
    public async Task FileProfileStoreRejectsCredentialContextChangesAtomically() =>
        await AssertProfileStoreRejectsCredentialContextChangeAsync(
            new FileMailProfileStore(CreateTemporaryFilePath("profiles.json")));

    private static async Task AssertProfileStoreRejectsProviderKindChangeAsync(IMailProfileStore store) {
        await store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "SMTP",
            Kind = MailProfileKind.Smtp
        });

        await Assert.ThrowsAsync<MailProfileKindChangeException>(() => store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Graph",
            Kind = MailProfileKind.Graph
        }));

        Assert.Equal(MailProfileKind.Smtp, (await store.GetByIdAsync("provider"))!.Kind);
    }

    private static async Task AssertProfileStoreRejectsCredentialContextChangeAsync(IMailProfileStore store) {
        await store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "JMAP",
            Kind = MailProfileKind.Jmap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://mail.example.com/.well-known/jmap"
            }
        });

        await Assert.ThrowsAsync<MailProfileCredentialContextChangeException>(() => store.SaveAsync(new MailProfile {
            Id = "provider",
            DisplayName = "Redirected JMAP",
            Kind = MailProfileKind.Jmap,
            Settings = new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://mail.attacker.example/.well-known/jmap"
            }
        }));

        Assert.Equal(
            "https://mail.example.com/.well-known/jmap",
            (await store.GetByIdAsync("provider"))!.Settings[MailProfileSettingsKeys.JmapSessionUrl]);
    }

    private static Dictionary<string, string> CreateValidSettings(MailProfileKind kind) =>
        kind == MailProfileKind.Jmap
            ? new Dictionary<string, string> {
                [MailProfileSettingsKeys.JmapSessionUrl] = "https://mail.example.com/.well-known/jmap"
            }
            : new Dictionary<string, string> {
                [MailProfileSettingsKeys.Server] = "mail.example.com"
            };

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

    private sealed class BasicSecretStore : IMailSecretStore {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _secrets.TryGetValue($"{profileId}::{secretName}", out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _secrets[$"{profileId}::{secretName}"] = secretValue;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.Remove($"{profileId}::{secretName}"));
    }
}
