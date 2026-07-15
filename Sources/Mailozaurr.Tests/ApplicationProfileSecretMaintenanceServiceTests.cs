using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileSecretMaintenanceServiceTests {
    [Fact]
    public async Task OrphanMaintenanceUsesTheCurrentProfileInventory() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new FileMailSecretStore(
            CreateTemporaryFilePath("secrets.json"),
            new TestCredentialProtector());
        var service = new MailProfileSecretMaintenanceService(profileStore, secretStore);
        await profileStore.SaveAsync(new MailProfile {
            Id = "work",
            DisplayName = "Work",
            Kind = MailProfileKind.Imap
        });
        await secretStore.SetSecretAsync("work", "password", "work-secret");
        await secretStore.SetSecretAsync("retired", "password", "retired-secret");

        MailProfileSecretMaintenanceResult inspection = await service.InspectOrphanedSecretsAsync();
        MailProfileSecretMaintenanceResult cleanup = await service.RemoveOrphanedSecretsAsync();

        Assert.True(inspection.Succeeded);
        Assert.Equal(new[] { "retired" }, inspection.OrphanedProfileIds);
        Assert.Equal(new[] { "retired" }, cleanup.RemovedProfileIds);
        Assert.Equal("work-secret", await secretStore.GetSecretAsync("work", "password"));
        Assert.Null(await secretStore.GetSecretAsync("retired", "password"));
    }

    [Fact]
    public async Task OrphanMaintenanceReportsUnsupportedCustomSecretStores() {
        var service = new MailProfileSecretMaintenanceService(
            new FileMailProfileStore(CreateTemporaryFilePath("profiles.json")),
            new MinimalSecretStore());

        MailProfileSecretMaintenanceResult result = await service.InspectOrphanedSecretsAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("secret_maintenance_unavailable", result.Code);
    }

    [Fact]
    public async Task CleanupRefusesAProfileStoreWithoutStableInventoryCoordination() {
        var service = new MailProfileSecretMaintenanceService(
            new MinimalProfileStore(),
            new FileMailSecretStore(
                CreateTemporaryFilePath("secrets.json"),
                new TestCredentialProtector()));

        MailProfileSecretMaintenanceResult result = await service.RemoveOrphanedSecretsAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("secret_cleanup_coordination_unavailable", result.Code);
    }

    [Fact]
    public async Task CleanupHoldsTheProfileInventoryStableUntilSecretRemovalCompletes() {
        var profileStore = new FileMailProfileStore(CreateTemporaryFilePath("profiles.json"));
        var secretStore = new BlockingMaintenanceSecretStore();
        var service = new MailProfileSecretMaintenanceService(profileStore, secretStore);

        Task<MailProfileSecretMaintenanceResult> cleanup = service.RemoveOrphanedSecretsAsync();
        Task entered = secretStore.Entered;
        Assert.Same(entered, await Task.WhenAny(entered, Task.Delay(TimeSpan.FromSeconds(5))));

        Task save = profileStore.SaveAsync(new MailProfile {
            Id = "created-during-cleanup",
            DisplayName = "Created during cleanup",
            Kind = MailProfileKind.Imap
        });
        try {
            await Task.Delay(100);
            Assert.False(save.IsCompleted);
        } finally {
            secretStore.Release();
        }

        await Task.WhenAll(cleanup, save);
        Assert.NotNull(await profileStore.GetByIdAsync("created-during-cleanup"));
    }

    private static string CreateTemporaryFilePath(string fileName) {
        string directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class TestCredentialProtector : ICredentialProtector {
        public string Protect(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));

        public string Unprotect(string protectedValue) {
            byte[] decoded = Convert.FromBase64String(protectedValue);
            return System.Text.Encoding.UTF8.GetString(decoded);
        }
    }

    private sealed class MinimalSecretStore : IMailSecretStore {
        public Task<string?> GetSecretAsync(
            string profileId,
            string secretName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetSecretAsync(
            string profileId,
            string secretName,
            string secretValue,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> RemoveSecretAsync(
            string profileId,
            string secretName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class MinimalProfileStore : IMailProfileStore {
        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(Array.Empty<MailProfile>());

        public Task<MailProfile?> GetByIdAsync(
            string profileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MailProfile?>(null);

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> RemoveAsync(
            string profileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class BlockingMaintenanceSecretStore :
        IMailSecretStore,
        IMailProfileSecretMaintenanceStore {
        private readonly TaskCompletionSource<bool> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Entered => _entered.Task;

        internal void Release() => _release.TrySetResult(true);

        public Task<string?> GetSecretAsync(
            string profileId,
            string secretName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetSecretAsync(
            string profileId,
            string secretName,
            string secretValue,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> RemoveSecretAsync(
            string profileId,
            string secretName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<MailProfileSecretMaintenanceResult> InspectOrphanedSecretsAsync(
            IReadOnlyCollection<string> knownProfileIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailProfileSecretMaintenanceResult { Succeeded = true });

        public async Task<MailProfileSecretMaintenanceResult> RemoveOrphanedSecretsAsync(
            IReadOnlyCollection<string> knownProfileIds,
            CancellationToken cancellationToken = default) {
            _entered.TrySetResult(true);
            await _release.Task.ConfigureAwait(false);
            return new MailProfileSecretMaintenanceResult { Succeeded = true };
        }
    }
}