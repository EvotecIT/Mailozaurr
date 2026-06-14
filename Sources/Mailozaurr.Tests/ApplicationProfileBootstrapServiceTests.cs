using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileBootstrapServiceTests {
    [Fact]
    public async Task SaveGraphProfileAsyncPersistsProfileSettingsAndSecrets() {
        var profileStore = new InMemoryProfileStore();
        var secretStore = new InMemorySecretStore();
        var service = new MailProfileBootstrapService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var result = await service.SaveGraphProfileAsync(new GraphProfileBootstrapRequest {
            ProfileId = "graph-work",
            DisplayName = "Work Graph",
            Mailbox = "shared@example.com",
            ClientId = "client-id",
            TenantId = "tenant-id",
            ClientSecret = "client-secret",
            IsDefault = true
        });

        var profile = await profileStore.GetByIdAsync("graph-work");
        var clientSecret = await secretStore.GetSecretAsync("graph-work", MailSecretNames.ClientSecret);

        Assert.True(result.Succeeded);
        Assert.NotNull(profile);
        Assert.Equal(MailProfileKind.Graph, profile!.Kind);
        Assert.Equal("shared@example.com", profile.DefaultMailbox);
        Assert.Equal("shared@example.com", profile.DefaultSender);
        Assert.True(profile.IsDefault);
        Assert.Equal("shared@example.com", profile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal("client-id", profile.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("tenant-id", profile.Settings[MailProfileSettingsKeys.TenantId]);
        Assert.Equal("client-secret", clientSecret);
    }

    [Fact]
    public async Task SaveGraphProfileAsyncSupportsSecretReferences() {
        var profileStore = new InMemoryProfileStore();
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("shared-secrets", MailSecretNames.ClientSecret, "shared-client-secret");
        var service = new MailProfileBootstrapService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var result = await service.SaveGraphProfileAsync(new GraphProfileBootstrapRequest {
            ProfileId = "graph-work",
            DisplayName = "Work Graph",
            Mailbox = "shared@example.com",
            ClientId = "client-id",
            TenantId = "tenant-id",
            ClientSecretReference = $"shared-secrets:{MailSecretNames.ClientSecret}"
        });

        var clientSecret = await secretStore.GetSecretAsync("graph-work", MailSecretNames.ClientSecret);

        Assert.True(result.Succeeded);
        Assert.Equal("shared-client-secret", clientSecret);
    }

    [Fact]
    public async Task SaveGraphProfileAsyncRequiresAuthenticationMaterial() {
        var profileStore = new InMemoryProfileStore();
        var secretStore = new InMemorySecretStore();
        var service = new MailProfileBootstrapService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var result = await service.SaveGraphProfileAsync(new GraphProfileBootstrapRequest {
            ProfileId = "graph-work",
            DisplayName = "Work Graph",
            Mailbox = "shared@example.com"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("graph_auth_required", result.Code);
    }

    [Fact]
    public async Task SaveGmailProfileAsyncPersistsProfileSettingsAndSecrets() {
        var profileStore = new InMemoryProfileStore();
        var secretStore = new InMemorySecretStore();
        var service = new MailProfileBootstrapService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var result = await service.SaveGmailProfileAsync(new GmailProfileBootstrapRequest {
            ProfileId = "gmail-work",
            DisplayName = "Work Gmail",
            Mailbox = "me@example.com",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RefreshToken = "refresh-token",
            IsDefault = true
        });

        var profile = await profileStore.GetByIdAsync("gmail-work");
        var clientSecret = await secretStore.GetSecretAsync("gmail-work", MailSecretNames.ClientSecret);
        var refreshToken = await secretStore.GetSecretAsync("gmail-work", MailSecretNames.RefreshToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(profile);
        Assert.Equal(MailProfileKind.Gmail, profile!.Kind);
        Assert.Equal("me@example.com", profile.DefaultMailbox);
        Assert.Equal("me@example.com", profile.DefaultSender);
        Assert.True(profile.IsDefault);
        Assert.Equal("me@example.com", profile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal("client-id", profile.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("client-secret", clientSecret);
        Assert.Equal("refresh-token", refreshToken);
    }

    [Fact]
    public async Task SaveGmailProfileAsyncRequiresAuthenticationMaterial() {
        var profileStore = new InMemoryProfileStore();
        var secretStore = new InMemorySecretStore();
        var service = new MailProfileBootstrapService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var result = await service.SaveGmailProfileAsync(new GmailProfileBootstrapRequest {
            ProfileId = "gmail-work",
            DisplayName = "Work Gmail"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("gmail_auth_required", result.Code);
    }

    [Fact]
    public async Task DiagnoseAsyncReportsMissingGmailSecrets() {
        var profileStore = new InMemoryProfileStore();
        var secretStore = new InMemorySecretStore();
        await profileStore.SaveAsync(new MailProfile {
            Id = "gmail-work",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultMailbox = "me@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "me@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        var service = new MailProfileService(profileStore, secretStore);

        var result = await service.DiagnoseAsync("gmail-work");

        Assert.False(result.Succeeded);
        Assert.Equal("profile_not_ready", result.Code);
        Assert.Contains(result.Errors, error => error.IndexOf("Gmail profiles need an access token", StringComparison.Ordinal) >= 0);
    }

    private sealed class InMemoryProfileStore : IMailProfileStore {
        private readonly Dictionary<string, MailProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(_profiles.Values.ToArray());

        public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
            _profiles.TryGetValue(profileId, out var profile);
            return Task.FromResult<MailProfile?>(profile);
        }

        public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Remove(profileId));

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            _profiles[profile.Id] = profile;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySecretStore : IMailSecretStore {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _values.TryGetValue($"{profileId}::{secretName}", out var value);
            return Task.FromResult<string?>(value);
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.Remove($"{profileId}::{secretName}"));

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _values[$"{profileId}::{secretName}"] = secretValue;
            return Task.CompletedTask;
        }
    }
}