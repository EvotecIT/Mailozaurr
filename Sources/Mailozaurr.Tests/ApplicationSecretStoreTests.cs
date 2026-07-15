using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationSecretStoreTests {
    [Fact]
    public async Task SecretStoreProtectsAndReturnsValues() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        var store = new FileMailSecretStore(filePath, protector);

        await store.SetSecretAsync("work-imap", "password", "super-secret");

        var loaded = await store.GetSecretAsync("work-imap", "password");
        var fileContent = File.ReadAllText(filePath);

        Assert.Equal("super-secret", loaded);
        Assert.DoesNotContain("super-secret", fileContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveSecretReturnsFalseWhenEntryDoesNotExist() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        var store = new FileMailSecretStore(filePath, protector);

        var removed = await store.RemoveSecretAsync("work-imap", "password");

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveSecretDeletesStoredValue() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        var store = new FileMailSecretStore(filePath, protector);

        await store.SetSecretAsync("work-imap", "password", "super-secret");
        var removed = await store.RemoveSecretAsync("work-imap", "password");
        var loaded = await store.GetSecretAsync("work-imap", "password");

        Assert.True(removed);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task UpdatingSecretReplacesFileWithoutLeavingTemporaryArtifacts() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        var store = new FileMailSecretStore(filePath, protector);

        await store.SetSecretAsync("work-imap", "password", "initial");
        await store.SetSecretAsync("work-imap", "password", "updated");

        var loaded = await store.GetSecretAsync("work-imap", "password");
        var files = Directory.GetFiles(Path.GetDirectoryName(filePath)!);

        Assert.Equal("updated", loaded);
        Assert.Single(files);
        Assert.Equal(filePath, files[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReloadedSecretKeysRemainCaseInsensitive() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        await new FileMailSecretStore(filePath, protector)
            .SetSecretAsync("Work-Imap", "Password", "super-secret");

        var loaded = await new FileMailSecretStore(filePath, protector)
            .GetSecretAsync("work-imap", "password");

        Assert.Equal("super-secret", loaded);
    }

    [Fact]
    public async Task RemoveProfileSecretsMatchesProfileIdExactly() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        var store = new FileMailSecretStore(filePath, protector);
        await store.SetSecretAsync("team", "archive::password", "team-secret");
        await store.SetSecretAsync("team::archive", "password", "archive-secret");

        await store.RemoveProfileSecretsAsync("team");

        Assert.Null(await store.GetSecretAsync("team", "archive::password"));
        Assert.Equal("archive-secret", await store.GetSecretAsync("team::archive", "password"));
    }

    [Fact]
    public async Task AmbiguousLegacyFlatSecretKeysRemainAvailableAfterMigration() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        string teamValue = protector.Protect("team-secret");
        string archiveValue = protector.Protect("archive-secret");
        File.WriteAllText(filePath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"team::password\":\"{teamValue}\"," +
            $"\"team::archive::password\":\"{archiveValue}\"}}}}");
        var store = new FileMailSecretStore(filePath, protector);

        await ((IMailProfileSecretContextCleanup)store).RemoveProfileSecretsAsync(
            "team",
            new[] { "team", "team::archive" });

        Assert.Null(await store.GetSecretAsync("team", "password"));
        Assert.Equal("archive-secret", await store.GetSecretAsync("team::archive", "password"));
    }

    [Fact]
    public async Task ProfileEnumerationDoesNotClaimAmbiguousLegacyKeysOwnedByLongerProfileIds() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        string teamValue = protector.Protect("team-secret");
        string archiveValue = protector.Protect("archive-secret");
        File.WriteAllText(filePath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"team::password\":\"{teamValue}\"," +
            $"\"team::archive::password\":\"{archiveValue}\"}}}}");
        var store = new FileMailSecretStore(filePath, protector);

        IReadOnlyDictionary<string, string> teamSecrets = await store.GetProfileSecretsAsync("team");

        Assert.Equal("team-secret", teamSecrets["password"]);
        Assert.DoesNotContain("archive::password", teamSecrets.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("archive-secret", await store.GetSecretAsync("team::archive", "password"));
    }

    [Fact]
    public async Task ContextFreeCleanupLeavesAmbiguousLegacyKeysForProfileAwareOwnership() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        string teamValue = protector.Protect("team-secret");
        string archiveValue = protector.Protect("archive-secret");
        File.WriteAllText(filePath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"team::password\":\"{teamValue}\"," +
            $"\"team::archive::password\":\"{archiveValue}\"}}}}");
        var store = new FileMailSecretStore(filePath, protector);

        await ((IMailProfileSecretCleanup)store).RemoveProfileSecretsAsync("team");

        Assert.Null(await store.GetSecretAsync("team", "password"));
        Assert.Equal("archive-secret", await store.GetSecretAsync("team::archive", "password"));
    }

    [Fact]
    public async Task ProfileAwareCleanupPurgesLegacySecretsForSeparatorBearingProfileId() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        string archiveValue = protector.Protect("archive-secret");
        File.WriteAllText(filePath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"team::archive::password\":\"{archiveValue}\"}}}}");
        var store = new FileMailSecretStore(filePath, protector);

        await ((IMailProfileSecretContextCleanup)store).RemoveProfileSecretsAsync(
            "team::archive",
            new[] { "team", "team::archive" });

        Assert.Null(await store.GetSecretAsync("team::archive", "password"));
    }

    [Fact]
    public async Task AmbiguousLegacyFlatSecretNameSurvivesAnUnrelatedWrite() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        string legacyValue = protector.Protect("legacy-secret");
        File.WriteAllText(filePath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"work::api::token\":\"{legacyValue}\"}}}}");
        var store = new FileMailSecretStore(filePath, protector);

        await store.SetSecretAsync("personal", "password", "new-secret");

        var reloaded = new FileMailSecretStore(filePath, protector);
        Assert.Equal("legacy-secret", await reloaded.GetSecretAsync("work", "api::token"));
        IReadOnlyDictionary<string, string> workSecrets = await reloaded.GetProfileSecretsAsync("work");
        Assert.DoesNotContain("api::token", workSecrets.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("new-secret", await reloaded.GetSecretAsync("personal", "password"));
    }

    [Fact]
    public async Task NativeSnapshotRestoresAnAmbiguousLegacySecretWithoutDecryptingIt() {
        var filePath = CreateTemporaryFilePath();
        var protector = new TestCredentialProtector();
        string legacyValue = protector.Protect("legacy-secret");
        File.WriteAllText(filePath,
            "{\"Version\":1,\"Secrets\":{" +
            $"\"work::api::token\":\"{legacyValue}\"}}}}");
        var store = new FileMailSecretStore(filePath, protector);
        var snapshotStore = (IMailProfileSecretSnapshotStore)store;
        IMailProfileSecretSnapshot snapshot = await snapshotStore.CaptureProfileSecretsAsync("work");

        await store.SetSecretAsync("work", "api::token", "replacement");
        await snapshotStore.RestoreProfileSecretsAsync("work", snapshot);

        Assert.Equal("legacy-secret", await store.GetSecretAsync("work", "api::token"));
    }

    private static string CreateTemporaryFilePath() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "secrets.json");
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
