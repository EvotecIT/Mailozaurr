#if NET8_0_OR_GREATER
using Mailozaurr;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    [Fact]
    public async Task MailProfilesListReturnsConfiguredProfiles() {
        using var fixture = new TestFixture();

        var profiles = await fixture.Tools.mail_profiles_list();

        var profile = Assert.Single(profiles);
        Assert.Equal("gmail-work", profile.Id);
        Assert.Equal(MailProfileKind.Gmail, profile.Kind);
    }

    [Fact]
    public async Task MailProfilesSummaryListReturnsAggregatedOverviews() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-work",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultSender = "user@example.com",
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("gmail-work", MailSecretNames.AccessToken, "token");

        var overviews = await fixture.Tools.mail_profiles_summary_list();

        var overview = Assert.Single(overviews);
        Assert.Equal("gmail-work", overview.Profile.Id);
        Assert.True(overview.SupportsRead);
        Assert.True(overview.SupportsSend);
        Assert.True(overview.IsReady);
        Assert.Equal("gmail-work", fixture.ProfileAuthService.LastStatusProfileId);
    }

    [Fact]
    public async Task MailProfilesSummaryCompactListReturnsLightweightProjection() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-work",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultSender = "user@example.com",
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("gmail-work", MailSecretNames.AccessToken, "token");

        var overviews = await fixture.Tools.mail_profiles_summary_compact_list();

        var overview = Assert.Single(overviews);
        Assert.Equal("gmail-work", overview.Id);
        Assert.Equal("interactive", overview.AuthMode);
        Assert.True(overview.IsReady);
    }

    [Fact]
    public async Task MailProfilesSummaryListSupportsSharedFilters() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "smtp-alerts",
            DisplayName = "Alerts SMTP",
            Kind = MailProfileKind.Smtp,
            DefaultSender = "alerts@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        var overviews = await fixture.Tools.mail_profiles_summary_list(kind: "smtp", canSendOnly: true);

        var overview = Assert.Single(overviews);
        Assert.Equal("smtp-alerts", overview.Profile.Id);
        Assert.True(overview.SupportsSend);
        Assert.False(overview.SupportsRead);
    }

    [Fact]
    public async Task MailProfilesSummaryListSupportsSharedSorting() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "a-smtp",
            DisplayName = "SMTP A",
            Kind = MailProfileKind.Smtp,
            DefaultSender = "alerts@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "z-gmail",
            DisplayName = "Gmail Z",
            Kind = MailProfileKind.Gmail,
            DefaultSender = "user@example.com",
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("z-gmail", MailSecretNames.AccessToken, "token");

        var overviews = await fixture.Tools.mail_profiles_summary_list(sortBy: "kind", descending: true);

        Assert.Equal(3, overviews.Count);
        Assert.Equal("a-smtp", overviews[0].Profile.Id);
    }

    [Fact]
    public async Task MailCapabilitiesGetReturnsEffectiveCapabilities() {
        using var fixture = new TestFixture();

        var capabilities = await fixture.Tools.mail_capabilities_get("gmail-work");

        Assert.Equal(MailProfileKind.Gmail, capabilities.Kind);
        Assert.True(capabilities.Supports(MailCapability.SendMessages));
        Assert.True(capabilities.Supports(MailCapability.SearchMessages));
    }

    [Fact]
    public async Task MailProfileSaveCreatesProfileWithSettings() {
        using var fixture = new TestFixture();

        var profile = await fixture.Tools.mail_profile_save(
            profileId: "graph-work",
            kind: "graph",
            displayName: "Work Graph",
            description: "Microsoft 365 profile",
            defaultSender: "graph@example.com",
            defaultMailbox: "graph@example.com",
            settings: new Dictionary<string, string> {
                ["tenant-id"] = "tenant-1",
                ["client-id"] = "client-1"
            });

        Assert.Equal("graph-work", profile.Id);
        Assert.Equal(MailProfileKind.Graph, profile.Kind);
        Assert.Equal("Microsoft 365 profile", profile.Description);
        Assert.Equal("tenant-1", profile.Settings["tenant-id"]);
        Assert.Equal("client-1", profile.Settings["client-id"]);
    }

    [Fact]
    public async Task MailProfileGraphBootstrapRejectsCrossProfileSecretsEvenWithLegacyConsent() {
        using var fixture = new TestFixture();
        await fixture.SecretStore.SetSecretAsync(
            "bootstrap-secrets", MailSecretNames.ClientSecret, "client-secret");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Tools.mail_profile_graph_bootstrap(
                profileId: "graph-work",
                displayName: "Work Graph",
                mailbox: "shared@example.com",
                clientId: "client-id",
                tenantId: "tenant-id",
                clientSecretReference: $"bootstrap-secrets:{MailSecretNames.ClientSecret}",
                allowCrossProfileSecretReferences: true));
        var storedSecret = await fixture.SecretStore.GetSecretAsync("graph-work", MailSecretNames.ClientSecret);

        Assert.Contains("crosses profile boundaries", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(storedSecret);
    }

    [Fact]
    public async Task MailProfileGraphBootstrapRejectsCrossProfileReferences() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "shared-secrets",
            DisplayName = "Shared Secrets",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "shared@example.com",
                [MailProfileSettingsKeys.ClientId] = "shared-client"
            }
        });
        await fixture.SecretStore.SetSecretAsync("shared-secrets", MailSecretNames.ClientSecret, "shared-client-secret");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Tools.mail_profile_graph_bootstrap(
                profileId: "graph-ref",
                displayName: "Graph Ref",
                mailbox: "shared@example.com",
                clientId: "client-id",
                tenantId: "tenant-id",
                clientSecretReference: $"shared-secrets:{MailSecretNames.ClientSecret}",
                allowCrossProfileSecretReferences: true));
        var storedSecret = await fixture.SecretStore.GetSecretAsync("graph-ref", MailSecretNames.ClientSecret);

        Assert.Contains("crosses profile boundaries", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(storedSecret);
    }

    [Fact]
    public async Task MailProfileGmailBootstrapRejectsCrossProfileSecrets() {
        using var fixture = new TestFixture();
        await fixture.SecretStore.SetSecretAsync(
            "bootstrap-secrets", MailSecretNames.ClientSecret, "client-secret");
        await fixture.SecretStore.SetSecretAsync(
            "bootstrap-secrets", MailSecretNames.RefreshToken, "refresh-token");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Tools.mail_profile_gmail_bootstrap(
                profileId: "gmail-work",
                displayName: "Work Gmail",
                mailbox: "me@example.com",
                clientId: "client-id",
                clientSecretReference: $"bootstrap-secrets:{MailSecretNames.ClientSecret}",
                refreshTokenReference: $"bootstrap-secrets:{MailSecretNames.RefreshToken}",
                allowCrossProfileSecretReferences: true));
        var clientSecret = await fixture.SecretStore.GetSecretAsync("gmail-work", MailSecretNames.ClientSecret);
        var refreshToken = await fixture.SecretStore.GetSecretAsync("gmail-work", MailSecretNames.RefreshToken);

        Assert.Contains("crosses profile boundaries", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(clientSecret);
        Assert.Null(refreshToken);
    }

    [Fact]
    public async Task MailProfileDoctorReportsMissingGmailAuthenticationMaterial() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-broken",
            DisplayName = "Broken Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultMailbox = "me@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "me@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });

        var result = await fixture.Tools.mail_profile_doctor("gmail-broken");

        Assert.False(result.Succeeded);
        Assert.Equal("profile_not_ready", result.Code);
        Assert.Contains(result.Errors, error => error.IndexOf("Gmail profiles need an access token", StringComparison.Ordinal) >= 0);
    }

    [Fact]
    public async Task MailProfileValidateRunsStructuralValidationOnly() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-structural",
            DisplayName = "Gmail Structural",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "me@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });

        var result = await fixture.Tools.mail_profile_validate("gmail-structural");

        Assert.True(result.Succeeded);
        Assert.Null(result.Code);
    }

    [Fact]
    public async Task MailProfileGmailLoginPersistsTokens() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-login",
            DisplayName = "Gmail Login",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@gmail.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("gmail-login", MailSecretNames.ClientSecret, "client-secret");

        var result = await fixture.Tools.mail_profile_gmail_login("gmail-login");
        var accessToken = await fixture.SecretStore.GetSecretAsync("gmail-login", MailSecretNames.AccessToken);
        var refreshToken = await fixture.SecretStore.GetSecretAsync("gmail-login", MailSecretNames.RefreshToken);

        Assert.True(result.Succeeded);
        Assert.Equal("gmail-login", result.ProfileId);
        Assert.Equal("gmail-access-token", accessToken);
        Assert.Equal("gmail-refresh-token", refreshToken);
        Assert.NotNull(fixture.ProfileAuthService.LastGmailRequest);
        Assert.Equal("user@gmail.com", fixture.ProfileAuthService.LastGmailRequest!.GmailAccount);
    }

    [Fact]
    public async Task MailProfileGmailLoginSupportsSameProfileSecretReference() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-login-ref",
            DisplayName = "Gmail Login Ref",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@gmail.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("gmail-login-ref", MailSecretNames.ClientSecret, "client-secret-from-store");

        var result = await fixture.Tools.mail_profile_gmail_login(
            "gmail-login-ref",
            clientSecretReference: MailSecretNames.ClientSecret);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.ProfileAuthService.LastGmailRequest);
        Assert.Equal("client-secret-from-store", fixture.ProfileAuthService.LastGmailRequest!.ClientSecret);
    }

    [Fact]
    public async Task MailProfileGraphLoginPersistsAccessToken() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "graph-login",
            DisplayName = "Graph Login",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.TenantId] = "tenant-id"
            }
        });

        var result = await fixture.Tools.mail_profile_graph_login("graph-login", login: "user@example.com");
        var accessToken = await fixture.SecretStore.GetSecretAsync("graph-login", MailSecretNames.AccessToken);
        var profile = await fixture.ProfileStore.GetByIdAsync("graph-login");

        Assert.True(result.Succeeded);
        Assert.Equal("graph-login", result.ProfileId);
        Assert.Equal("graph-access-token", accessToken);
        Assert.NotNull(profile);
        Assert.Equal("user@example.com", profile!.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.NotNull(fixture.ProfileAuthService.LastGraphRequest);
        Assert.Equal("user@example.com", fixture.ProfileAuthService.LastGraphRequest!.Login);
    }

    [Fact]
    public async Task MailProfileRefreshAuthDelegatesToSharedAuthService() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-refresh",
            DisplayName = "Gmail Refresh",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "refresh@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("gmail-refresh", MailSecretNames.ClientSecret, "client-secret");

        var result = await fixture.Tools.mail_profile_refresh_auth("gmail-refresh");

        Assert.True(result.Succeeded);
        Assert.Equal(1, fixture.ProfileAuthService.RefreshCalls);
        Assert.Equal("gmail-refresh", fixture.ProfileAuthService.LastRefreshProfileId);
    }

    [Fact]
    public async Task MailProfileAuthStatusDelegatesToSharedAuthService() {
        using var fixture = new TestFixture();

        var status = await fixture.Tools.mail_profile_auth_status("gmail-work");

        Assert.Equal("gmail-work", status.ProfileId);
        Assert.Equal("gmail-work", fixture.ProfileAuthService.LastStatusProfileId);
        Assert.Equal("interactive", status.Mode);
    }

    [Fact]
    public async Task MailProfileSummaryAggregatesSharedOverview() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-work",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultSender = "user@example.com",
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("gmail-work", MailSecretNames.AccessToken, "token");

        var overview = await fixture.Tools.mail_profile_summary("gmail-work");

        Assert.Equal("gmail-work", overview.Profile.Id);
        Assert.True(overview.SupportsRead);
        Assert.True(overview.SupportsSend);
        Assert.True(overview.IsReady);
        Assert.Equal("gmail-work", fixture.ProfileAuthService.LastStatusProfileId);
    }

    [Fact]
    public async Task MailProfileSummaryCompactReturnsLightweightProjection() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-work",
            DisplayName = "Work Gmail",
            Kind = MailProfileKind.Gmail,
            DefaultSender = "user@example.com",
            DefaultMailbox = "user@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@example.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("gmail-work", MailSecretNames.AccessToken, "token");

        var overview = await fixture.Tools.mail_profile_summary_compact("gmail-work");

        Assert.Equal("gmail-work", overview.Id);
        Assert.Equal("Work Gmail", overview.DisplayName);
        Assert.True(overview.SupportsSend);
        Assert.True(overview.IsReady);
    }

    [Fact]
    public async Task MailProfileTestDelegatesToSharedConnectionService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_profile_test("gmail-work");

        Assert.True(result.Succeeded);
        Assert.Equal("gmail-work", fixture.ProfileConnectionService.LastProfileId);
        Assert.Equal("getProfile", result.Probe);
        var stage = Assert.Single(result.Stages);
        Assert.Equal(MailProfileConnectionTestPhase.Profile, stage.Phase);
        Assert.Equal("Gmail", stage.Evidence?.Protocol);
        Assert.Equal("user@example.com", stage.Evidence?.Identity?.EmailAddress);
    }

    [Fact]
    public async Task MailProfileTestParsesRequestedScope() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_profile_test("gmail-work", scope: "send");

        Assert.True(result.Succeeded);
        Assert.Equal(MailProfileConnectionTestScope.Send, fixture.ProfileConnectionService.LastScope);
        Assert.Equal(MailProfileConnectionTestScope.Send, result.RequestedScope);
    }

    [Fact]
    public async Task MailProfileSetDefaultMarksProfileAsDefault() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_profile_save(
            profileId: "graph-work",
            kind: "graph",
            displayName: "Work Graph");

        var result = await fixture.Tools.mail_profile_set_default("graph-work");
        var profile = await fixture.Tools.mail_profile_get("graph-work");

        Assert.True(result.Succeeded);
        Assert.True(profile.IsDefault);
    }

    [Fact]
    public async Task MailProfileSecretCopyRejectsCrossProfileReference() {
        using var fixture = new TestFixture();
        await fixture.SecretStore.SetSecretAsync("source", "refresh-token", "secret-value");

        var setResult = await fixture.Tools.mail_profile_secret_copy(
            "gmail-work", "refresh-token", "source:refresh-token");
        var storedSecret = await fixture.SecretStore.GetSecretAsync("gmail-work", "refresh-token");
        Assert.False(setResult.Succeeded);
        Assert.Contains("crosses profile boundaries", setResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(storedSecret);
    }

    [Fact]
    public async Task MailProfileSecretCopyRejectsCrossProfileReferenceWithMatchingName() {
        using var fixture = new TestFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "shared-secrets",
            DisplayName = "Shared Secrets",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "shared@example.com",
                [MailProfileSettingsKeys.ClientId] = "shared-client"
            }
        });
        await fixture.SecretStore.SetSecretAsync("shared-secrets", MailSecretNames.RefreshToken, "copied-secret");

        var setResult = await fixture.Tools.mail_profile_secret_copy(
            profileId: "gmail-work",
            secretName: MailSecretNames.RefreshToken,
            secretReference: $"shared-secrets:{MailSecretNames.RefreshToken}");
        var storedSecret = await fixture.SecretStore.GetSecretAsync("gmail-work", MailSecretNames.RefreshToken);

        Assert.False(setResult.Succeeded);
        Assert.Contains("crosses profile boundaries", setResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(storedSecret);
    }

    [Fact]
    public async Task MailProfileOrphanSecretToolsInspectAndCleanThroughTheSharedService() {
        using var fixture = new TestFixture();
        await fixture.SecretStore.SetSecretAsync("retired", "password", "retired-secret");

        MailProfileSecretMaintenanceResult inspection =
            await fixture.Tools.mail_profile_secrets_orphaned_inspect();
        MailProfileSecretMaintenanceResult cleanup =
            await fixture.Tools.mail_profile_secrets_orphaned_cleanup();

        Assert.Equal(new[] { "retired" }, inspection.OrphanedProfileIds);
        Assert.Equal(new[] { "retired" }, cleanup.RemovedProfileIds);
        Assert.Null(await fixture.SecretStore.GetSecretAsync("retired", "password"));
    }

    [Fact]
    public async Task MailProfileDeleteRemovesProfile() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_profile_save(
            profileId: "graph-work",
            kind: "graph",
            displayName: "Work Graph");

        var result = await fixture.Tools.mail_profile_delete("graph-work");
        var profiles = await fixture.Tools.mail_profiles_list();

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(profiles, profile => profile.Id == "graph-work");
    }
}
#endif
