using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationProfileOverviewServiceTests {
    [Fact]
    public async Task GetOverviewAggregatesCapabilitiesAuthAndReadiness() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-work",
                DisplayName = "Work Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultSender = "user@example.com",
                DefaultMailbox = "user@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                    [MailProfileSettingsKeys.ClientId] = "client-id"
                }
            }
        });
        var secretStore = new InMemorySecretStore();
        await secretStore.SetSecretAsync("gmail-work", MailSecretNames.AccessToken, "token");
        var profiles = new MailProfileService(profileStore, secretStore);
        var auth = new FakeProfileAuthService();
        var service = new MailProfileOverviewService(profiles, auth);

        var overview = await service.GetOverviewAsync("gmail-work");

        Assert.NotNull(overview);
        Assert.Equal("gmail-work", overview!.Profile.Id);
        Assert.True(overview.SupportsRead);
        Assert.True(overview.SupportsSend);
        Assert.True(overview.IsReady);
        Assert.Equal(0, overview.ErrorCount);
        Assert.Equal(0, overview.WarningCount);
        Assert.NotNull(overview.Capabilities);
        Assert.NotNull(overview.AuthStatus);
        Assert.Contains("read=yes", overview.Summary, StringComparison.Ordinal);
        Assert.Contains("send=yes", overview.Summary, StringComparison.Ordinal);
        Assert.Contains("auth=interactive", overview.Summary, StringComparison.Ordinal);
        Assert.Contains("readiness=ready", overview.Summary, StringComparison.Ordinal);
        Assert.Equal("gmail-work", auth.LastProfileId);
    }

    [Fact]
    public async Task GetOverviewReturnsNullWhenProfileDoesNotExist() {
        var profiles = new MailProfileService(new InMemoryProfileStore(Array.Empty<MailProfile>()), new InMemorySecretStore());
        var service = new MailProfileOverviewService(profiles, new FakeProfileAuthService());

        var overview = await service.GetOverviewAsync("missing");

        Assert.Null(overview);
    }

    [Fact]
    public async Task GetOverviewsReturnsAggregatedSummariesForAllProfiles() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "imap-work",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                IsDefault = true,
                DefaultMailbox = "work@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Server] = "imap.example.com"
                }
            },
            new MailProfile {
                Id = "smtp-alerts",
                DisplayName = "Alerts SMTP",
                Kind = MailProfileKind.Smtp,
                DefaultSender = "alerts@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Server] = "smtp.example.com"
                }
            }
        });
        var profiles = new MailProfileService(profileStore, new InMemorySecretStore());
        var auth = new FakeProfileAuthService();
        var service = new MailProfileOverviewService(profiles, auth);

        var overviews = await service.GetOverviewsAsync();

        Assert.Equal(2, overviews.Count);
        Assert.Contains(overviews, overview => overview.Profile.Id == "imap-work" && overview.SupportsRead);
        Assert.Contains(overviews, overview => overview.Profile.Id == "smtp-alerts" && overview.SupportsSend);
    }

    [Fact]
    public async Task GetOverviewsAppliesSharedFilters() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "imap-work",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                IsDefault = true,
                DefaultMailbox = "work@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Server] = "imap.example.com"
                }
            },
            new MailProfile {
                Id = "smtp-alerts",
                DisplayName = "Alerts SMTP",
                Kind = MailProfileKind.Smtp,
                DefaultSender = "alerts@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Server] = "smtp.example.com"
                }
            }
        });
        var profiles = new MailProfileService(profileStore, new InMemorySecretStore());
        var service = new MailProfileOverviewService(profiles, new FakeProfileAuthService());

        var overviews = await service.GetOverviewsAsync(new MailProfileOverviewQuery {
            CanSendOnly = true,
            DefaultOnly = false,
            Kind = MailProfileKind.Smtp
        });

        var overview = Assert.Single(overviews);
        Assert.Equal("smtp-alerts", overview.Profile.Id);
        Assert.True(overview.SupportsSend);
    }

    [Fact]
    public async Task GetOverviewsSupportsReadinessSorting() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "gmail-broken",
                DisplayName = "Broken Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "broken@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Mailbox] = "broken@example.com",
                    [MailProfileSettingsKeys.ClientId] = "client-id"
                }
            },
            new MailProfile {
                Id = "smtp-ready",
                DisplayName = "Ready SMTP",
                Kind = MailProfileKind.Smtp,
                DefaultSender = "alerts@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Server] = "smtp.example.com"
                }
            }
        });
        var secretStore = new InMemorySecretStore();
        var profiles = new MailProfileService(profileStore, secretStore);
        var service = new MailProfileOverviewService(profiles, new FakeProfileAuthService());

        var overviews = await service.GetOverviewsAsync(new MailProfileOverviewQuery {
            SortBy = MailProfileOverviewSortBy.Readiness
        });

        Assert.Equal(2, overviews.Count);
        Assert.Equal("gmail-broken", overviews[0].Profile.Id);
        Assert.False(overviews[0].IsReady);
        Assert.Equal("smtp-ready", overviews[1].Profile.Id);
        Assert.True(overviews[1].IsReady);
    }

    [Fact]
    public async Task GetCompactOverviewsReturnsLightweightProjection() {
        var profileStore = new InMemoryProfileStore(new[] {
            new MailProfile {
                Id = "imap-work",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                DefaultMailbox = "work@example.com",
                Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    [MailProfileSettingsKeys.Server] = "imap.example.com"
                }
            }
        });
        var profiles = new MailProfileService(profileStore, new InMemorySecretStore());
        var service = new MailProfileOverviewService(profiles, new FakeProfileAuthService());

        var overviews = await service.GetCompactOverviewsAsync();

        var overview = Assert.Single(overviews);
        Assert.Equal("imap-work", overview.Id);
        Assert.Equal("Work IMAP", overview.DisplayName);
        Assert.Equal(MailProfileKind.Imap, overview.Kind);
        Assert.True(overview.SupportsRead);
        Assert.False(overview.SupportsSend);
    }

    private sealed class FakeProfileAuthService : IMailProfileAuthService {
        public string? LastProfileId { get; private set; }

        public Task<MailProfileAuthStatus?> GetStatusAsync(string profileId, CancellationToken cancellationToken = default) {
            LastProfileId = profileId;
            return Task.FromResult<MailProfileAuthStatus?>(new MailProfileAuthStatus {
                ProfileId = profileId,
                ProfileKind = MailProfileKind.Gmail,
                Mode = "interactive",
                Mailbox = "user@example.com",
                HasAccessToken = true,
                CanRefresh = true,
                CanLoginInteractively = true,
                Summary = $"{profileId} auth status available."
            });
        }

        public Task<MailProfileAuthenticationResult> LoginGmailAsync(GmailProfileLoginRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MailProfileAuthenticationResult> LoginGraphAsync(GraphProfileLoginRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MailProfileAuthenticationResult> RefreshAsync(string profileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            _profiles[profile.Id] = CloneProfile(profile);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Remove(profileId));

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
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _secrets.TryGetValue(CreateKey(profileId, secretName), out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _secrets[CreateKey(profileId, secretName)] = secretValue;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.Remove(CreateKey(profileId, secretName)));

        private static string CreateKey(string profileId, string secretName) => $"{profileId}::{secretName}";
    }
}
