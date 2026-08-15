using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class ApplicationInMemoryStoreTests {
    [Fact]
    public async Task ProfileStoreClonesValuesAndKeepsSingleDefault() {
        var store = new InMemoryMailProfileStore();
        var first = new MailProfile {
            Id = "first",
            DisplayName = "First",
            IsDefault = true,
            Settings = new Dictionary<string, string> {
                ["mode"] = "one"
            }
        };
        await store.SaveAsync(first);

        first.DisplayName = "Changed outside store";
        first.Settings["mode"] = "changed";
        await store.SaveAsync(new MailProfile {
            Id = "second",
            DisplayName = "Second",
            IsDefault = true
        });

        MailProfile storedFirst = Assert.IsType<MailProfile>(await store.GetByIdAsync("FIRST"));
        MailProfile storedSecond = Assert.IsType<MailProfile>(await store.GetByIdAsync("second"));
        Assert.Equal("First", storedFirst.DisplayName);
        Assert.Equal("one", storedFirst.Settings["mode"]);
        Assert.False(storedFirst.IsDefault);
        Assert.True(storedSecond.IsDefault);

        storedFirst.Settings["mode"] = "mutated";
        Assert.Equal("one", (await store.GetByIdAsync("first"))!.Settings["mode"]);
    }

    [Fact]
    public async Task ProfileStoreStableInventoryBlocksConcurrentMutation() {
        var store = new InMemoryMailProfileStore();
        await store.SaveAsync(new MailProfile { Id = "first", DisplayName = "First" });
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string[]> inventory = store.ExecuteWithStableProfileIdsAsync(async (ids, cancellationToken) => {
            entered.SetResult(true);
            cancellationToken.ThrowIfCancellationRequested();
            await release.Task;
            return ids.ToArray();
        });
        await entered.Task;

        Task save = store.SaveAsync(new MailProfile { Id = "second", DisplayName = "Second" });
        Assert.False(save.IsCompleted);

        release.SetResult(true);
        Assert.Equal(new[] { "first" }, await inventory);
        await save;
        Assert.NotNull(await store.GetByIdAsync("second"));
    }

    [Fact]
    public async Task SecretStoreKeepsProfilesIsolatedAndSupportsCleanup() {
        var store = new InMemoryMailSecretStore();
        await store.SetSecretAsync("first", MailSecretNames.ClientSecret, "secret-one");
        await store.SetSecretAsync("second", MailSecretNames.ClientSecret, "secret-two");

        Assert.Equal("secret-one", await store.GetSecretAsync("FIRST", MailSecretNames.ClientSecret));
        Assert.Equal("secret-two", await store.GetSecretAsync("second", MailSecretNames.ClientSecret));

        IReadOnlyDictionary<string, string> snapshot = await store.GetProfileSecretsAsync("first");
        Assert.Equal("secret-one", snapshot[MailSecretNames.ClientSecret]);

        await store.RemoveProfileSecretsAsync("first");
        Assert.Null(await store.GetSecretAsync("first", MailSecretNames.ClientSecret));
        Assert.Equal("secret-two", await store.GetSecretAsync("second", MailSecretNames.ClientSecret));
    }
}
