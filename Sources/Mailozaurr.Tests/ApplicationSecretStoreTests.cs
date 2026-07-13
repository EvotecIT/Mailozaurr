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
