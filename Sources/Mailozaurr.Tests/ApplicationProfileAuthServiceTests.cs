using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileAuthServiceTests {
    [Fact]
    public async Task GetStatusAsyncReportsInteractiveGmailProfileState() {
        var expiresOn = DateTimeOffset.UtcNow.AddHours(2);
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "user@gmail.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Mailbox] = "user@gmail.com",
                    [MailProfileSettingsKeys.ClientId] = "client-id",
                    [MailProfileSettingsKeys.AuthFlow] = MailProfileAuthFlowNames.Interactive,
                    [MailProfileSettingsKeys.LoginHint] = "user@gmail.com",
                    [MailProfileSettingsKeys.TokenExpiresOn] = expiresOn.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                }
            }
        });
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("gmail-work", MailSecretNames.AccessToken, "access-token");
        await secretStore.SetSecretAsync("gmail-work", MailSecretNames.RefreshToken, "refresh-token");
        await secretStore.SetSecretAsync("gmail-work", MailSecretNames.ClientSecret, "client-secret");
        var service = new MailProfileAuthService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var status = await service.GetStatusAsync("gmail-work");

        Assert.NotNull(status);
        Assert.Equal("interactive", status!.Mode);
        Assert.Equal(MailProfileKind.Gmail, status.ProfileKind);
        Assert.Equal("user@gmail.com", status.Mailbox);
        Assert.True(status.HasAccessToken);
        Assert.True(status.HasRefreshToken);
        Assert.True(status.HasClientSecret);
        Assert.True(status.CanRefresh);
        Assert.True(status.CanLoginInteractively);
        Assert.False(status.IsTokenExpired);
        Assert.Equal(expiresOn, status.TokenExpiresOn);
    }

    [Fact]
    public async Task GetStatusAsyncReportsGraphAppOnlyState() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-app",
                DisplayName = "Graph App",
                Kind = MailProfileKind.Graph,
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.ClientId] = "client-id",
                    [MailProfileSettingsKeys.TenantId] = "tenant-id",
                    [MailProfileSettingsKeys.CertificatePath] = "C:\\certs\\graph.pfx"
                }
            }
        });
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("graph-app", MailSecretNames.ClientSecret, "client-secret");
        var service = new MailProfileAuthService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var status = await service.GetStatusAsync("graph-app");

        Assert.NotNull(status);
        Assert.Equal("appOnly", status!.Mode);
        Assert.True(status.HasClientId);
        Assert.True(status.HasTenantId);
        Assert.True(status.HasClientSecret);
        Assert.True(status.HasCertificatePath);
        Assert.False(status.CanRefresh);
        Assert.True(status.CanLoginInteractively);
    }

    [Fact]
    public async Task LoginGmailAsyncPersistsTokensAndProfileSettings() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Mailbox] = "user@gmail.com",
                    [MailProfileSettingsKeys.ClientId] = "client-id"
                }
            }
        });
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("gmail-work", MailSecretNames.ClientSecret, "client-secret");
        var service = new MailProfileAuthService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore,
            (request, _) => Task.FromResult(new OAuthCredential {
                UserName = request.GmailAccount!,
                AccessToken = "gmail-access-token",
                RefreshToken = "gmail-refresh-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            }));

        var result = await service.LoginGmailAsync(new GmailProfileLoginRequest {
            ProfileId = "gmail-work"
        });

        var profile = await profileStore.GetByIdAsync("gmail-work");
        var accessToken = await secretStore.GetSecretAsync("gmail-work", MailSecretNames.AccessToken);
        var refreshToken = await secretStore.GetSecretAsync("gmail-work", MailSecretNames.RefreshToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(profile);
        Assert.Equal("user@gmail.com", profile!.DefaultMailbox);
        Assert.Equal("user@gmail.com", profile.DefaultSender);
        Assert.Equal(MailProfileAuthFlowNames.Interactive, profile.Settings[MailProfileSettingsKeys.AuthFlow]);
        Assert.Equal("user@gmail.com", profile.Settings[MailProfileSettingsKeys.LoginHint]);
        Assert.True(profile.Settings.ContainsKey(MailProfileSettingsKeys.TokenExpiresOn));
        Assert.Equal("gmail-access-token", accessToken);
        Assert.Equal("gmail-refresh-token", refreshToken);
    }

    [Fact]
    public async Task LoginGraphAsyncPersistsAccessTokenAndProfileSettings() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "graph-work",
                DisplayName = "Work Graph",
                Kind = MailProfileKind.Graph,
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.ClientId] = "client-id",
                    [MailProfileSettingsKeys.TenantId] = "tenant-id"
                }
            }
        });
        var secretStore = new InMemorySecretStore();
        var service = new MailProfileAuthService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore,
            loginGraphAsync: (request, _) => Task.FromResult(new OAuthCredential {
                UserName = request.Login ?? "user@example.com",
                AccessToken = "graph-access-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            }));

        var result = await service.LoginGraphAsync(new GraphProfileLoginRequest {
            ProfileId = "graph-work",
            Login = "user@example.com"
        });

        var profile = await profileStore.GetByIdAsync("graph-work");
        var accessToken = await secretStore.GetSecretAsync("graph-work", MailSecretNames.AccessToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(profile);
        Assert.Equal("user@example.com", profile!.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal(MailProfileAuthFlowNames.Interactive, profile.Settings[MailProfileSettingsKeys.AuthFlow]);
        Assert.Equal("user@example.com", profile.Settings[MailProfileSettingsKeys.LoginHint]);
        Assert.True(profile.Settings.ContainsKey(MailProfileSettingsKeys.TokenExpiresOn));
        Assert.Equal("https://login.microsoftonline.com/common/oauth2/nativeclient", profile.Settings[MailProfileSettingsKeys.RedirectUri]);
        Assert.Equal("graph-access-token", accessToken);
    }

    [Fact]
    public async Task RefreshAsyncRoutesToSavedGmailProfile() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Mailbox] = "user@gmail.com",
                    [MailProfileSettingsKeys.ClientId] = "client-id"
                }
            }
        });
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("gmail-work", MailSecretNames.ClientSecret, "client-secret");
        var service = new MailProfileAuthService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore,
            (request, _) => Task.FromResult(new OAuthCredential {
                UserName = request.GmailAccount!,
                AccessToken = "gmail-access-token",
                RefreshToken = "gmail-refresh-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            }));

        var result = await service.RefreshAsync("gmail-work");

        var accessToken = await secretStore.GetSecretAsync("gmail-work", MailSecretNames.AccessToken);
        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileKind.Gmail, result.ProfileKind);
        Assert.Equal("gmail-access-token", accessToken);
    }

    [Fact]
    public async Task RefreshAsyncReturnsUnsupportedForNonOauthProfiles() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "smtp-work",
                DisplayName = "Work SMTP",
                Kind = MailProfileKind.Smtp
            }
        });
        var secretStore = new InMemorySecretStore();
        var service = new MailProfileAuthService(
            new MailProfileService(profileStore, secretStore),
            new MailProfileSecretService(profileStore, secretStore),
            secretStore);

        var result = await service.RefreshAsync("smtp-work");

        Assert.False(result.Succeeded);
        Assert.Equal("refresh_not_supported", result.Code);
    }

    private sealed class InMemoryProfileStore : IMailProfileStore {
        private readonly Dictionary<string, MailProfile> _profiles;

        public InMemoryProfileStore(IEnumerable<MailProfile> profiles) {
            _profiles = profiles.ToDictionary(profile => profile.Id, CloneProfile, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(_profiles.Values.Select(CloneProfile).ToArray());

        public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
            _profiles.TryGetValue(profileId, out var profile);
            return Task.FromResult(profile == null ? null : CloneProfile(profile));
        }

        public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Remove(profileId));

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            _profiles[profile.Id] = CloneProfile(profile);
            return Task.CompletedTask;
        }

        private static MailProfile CloneProfile(MailProfile profile) => new() {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            Kind = profile.Kind,
            DefaultSender = profile.DefaultSender,
            DefaultMailbox = profile.DefaultMailbox,
            IsDefault = profile.IsDefault,
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase),
            Capabilities = profile.Capabilities == null
                ? null
                : new ProfileCapabilities(profile.Capabilities.Kind, profile.Capabilities.Capabilities)
        };
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
