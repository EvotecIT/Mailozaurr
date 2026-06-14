using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileStoreTests {
    [Fact]
    public async Task FileProfileStoreRoundTripsProfiles() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);

        var profile = new MailProfile {
            Id = "work-imap",
            DisplayName = "Work IMAP",
            Kind = MailProfileKind.Imap,
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["server"] = "imap.example.com"
            }
        };

        await store.SaveAsync(profile);
        var loaded = await store.GetByIdAsync(profile.Id);
        var all = await store.GetAllAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Work IMAP", loaded!.DisplayName);
        Assert.Equal("imap.example.com", loaded.Settings["server"]);
        Assert.Single(all);
    }

    [Fact]
    public async Task SavingDefaultProfileClearsPreviousDefault() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);

        await store.SaveAsync(new MailProfile {
            Id = "one",
            DisplayName = "One",
            Kind = MailProfileKind.Imap,
            IsDefault = true
        });
        await store.SaveAsync(new MailProfile {
            Id = "two",
            DisplayName = "Two",
            Kind = MailProfileKind.Graph,
            IsDefault = true
        });

        var all = await store.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.False(all.Single(p => p.Id == "one").IsDefault);
        Assert.True(all.Single(p => p.Id == "two").IsDefault);
    }

    [Fact]
    public async Task RemoveReturnsFalseWhenProfileDoesNotExist() {
        var filePath = CreateTemporaryFilePath();
        var store = new FileMailProfileStore(filePath);

        var removed = await store.RemoveAsync("missing");

        Assert.False(removed);
    }

    private static string CreateTemporaryFilePath() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "profiles.json");
    }
}