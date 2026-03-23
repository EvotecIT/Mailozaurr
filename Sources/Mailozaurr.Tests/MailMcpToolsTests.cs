#if NET8_0_OR_GREATER
using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed class MailMcpToolsTests {
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
    public async Task MailProfileGraphBootstrapCreatesProfileAndStoresSecrets() {
        using var fixture = new TestFixture();

        var profile = await fixture.Tools.mail_profile_graph_bootstrap(
            profileId: "graph-work",
            displayName: "Work Graph",
            mailbox: "shared@example.com",
            clientId: "client-id",
            tenantId: "tenant-id",
            clientSecret: "client-secret");
        var storedSecret = await fixture.SecretStore.GetSecretAsync("graph-work", MailSecretNames.ClientSecret);

        Assert.Equal("graph-work", profile.Id);
        Assert.Equal(MailProfileKind.Graph, profile.Kind);
        Assert.Equal("shared@example.com", profile.DefaultMailbox);
        Assert.Equal("shared@example.com", profile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal("client-id", profile.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("tenant-id", profile.Settings[MailProfileSettingsKeys.TenantId]);
        Assert.Equal("client-secret", storedSecret);
    }

    [Fact]
    public async Task MailProfileGmailBootstrapCreatesProfileAndStoresSecrets() {
        using var fixture = new TestFixture();

        var profile = await fixture.Tools.mail_profile_gmail_bootstrap(
            profileId: "gmail-work",
            displayName: "Work Gmail",
            mailbox: "me@example.com",
            clientId: "client-id",
            clientSecret: "client-secret",
            refreshToken: "refresh-token");
        var clientSecret = await fixture.SecretStore.GetSecretAsync("gmail-work", MailSecretNames.ClientSecret);
        var refreshToken = await fixture.SecretStore.GetSecretAsync("gmail-work", MailSecretNames.RefreshToken);

        Assert.Equal("gmail-work", profile.Id);
        Assert.Equal(MailProfileKind.Gmail, profile.Kind);
        Assert.Equal("me@example.com", profile.DefaultMailbox);
        Assert.Equal("me@example.com", profile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal("client-id", profile.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("client-secret", clientSecret);
        Assert.Equal("refresh-token", refreshToken);
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
        Assert.Contains(result.Errors, error => error.Contains("Gmail profiles need an access token", StringComparison.Ordinal));
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
    public async Task MailProfileSecretSetAndRemoveUseSharedSecretStore() {
        using var fixture = new TestFixture();

        var setResult = await fixture.Tools.mail_profile_secret_set("gmail-work", "refresh-token", "secret-value");
        var storedSecret = await fixture.SecretStore.GetSecretAsync("gmail-work", "refresh-token");
        var removeResult = await fixture.Tools.mail_profile_secret_remove("gmail-work", "refresh-token");
        var removedSecret = await fixture.SecretStore.GetSecretAsync("gmail-work", "refresh-token");

        Assert.True(setResult.Succeeded);
        Assert.Equal("secret-value", storedSecret);
        Assert.True(removeResult.Succeeded);
        Assert.Null(removedSecret);
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

    [Fact]
    public async Task MailSearchDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_search(
            "gmail-work",
            mailboxId: "primary",
            folderId: "Inbox",
            queryText: "invoice",
            subjectContains: "Quarterly",
            fromContains: "billing@example.com",
            toContains: "team@example.com",
            hasAttachments: true,
            limit: 5);

        var result = Assert.Single(results);
        Assert.Equal("message-1", result.Id);
        Assert.NotNull(fixture.ReadService.LastSearchRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSearchRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSearchRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSearchRequest.FolderId);
        Assert.Equal("invoice", fixture.ReadService.LastSearchRequest.QueryText);
        Assert.True(fixture.ReadService.LastSearchRequest.HasAttachments);
        Assert.Equal(5, fixture.ReadService.LastSearchRequest.Limit);
    }

    [Fact]
    public async Task MailFoldersCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_folders_compact_list(
            "gmail-work",
            mailboxId: "primary",
            parentFolderId: "root",
            rootOnly: true);

        var result = Assert.Single(results);
        Assert.Equal("Inbox", result.Id);
        Assert.NotNull(fixture.ReadService.LastFolderCompactQuery);
        Assert.Equal("gmail-work", fixture.ReadService.LastFolderCompactQuery!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastFolderCompactQuery.MailboxId);
        Assert.Equal("root", fixture.ReadService.LastFolderCompactQuery.ParentFolderId);
        Assert.True(fixture.ReadService.LastFolderCompactQuery.RootOnly);
    }

    [Fact]
    public async Task MailFolderAliasesListDelegatesToSharedFolderAliasService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_folder_aliases_list("gmail-work", mailboxId: "primary");

        var archive = Assert.Single(results, result => result.Alias == MailFolderAliases.Archive);
        Assert.True(archive.IsResolved);
        Assert.Equal("archive", archive.FolderId);
        Assert.NotNull(fixture.ReadService.LastFolderQuery);
        Assert.Equal("gmail-work", fixture.ReadService.LastFolderQuery!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastFolderQuery.MailboxId);
    }

    [Fact]
    public async Task MailFolderResolveDelegatesToSharedFolderAliasService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_folder_resolve("gmail-work", "archive", mailboxId: "primary");

        Assert.True(result.IsAlias);
        Assert.Equal(MailFolderAliases.Archive, result.Alias);
        Assert.Equal("archive", result.EffectiveFolderId);
        Assert.NotNull(fixture.ReadService.LastFolderQuery);
        Assert.Equal("gmail-work", fixture.ReadService.LastFolderQuery!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastFolderQuery.MailboxId);
    }

    [Fact]
    public async Task MailMovePreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_move_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            "archive",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.NotNull(result.Destination);
        Assert.Equal("archive", result.Destination!.EffectiveFolderId);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailActionsPreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_actions_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.IncludedActionCount);
        Assert.Equal(4, result.SucceededActionCount);
        Assert.Equal(0, result.FailedActionCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Contains(result.Actions, action => action.Action == "archive" && action.Destination!.EffectiveFolderId == "archive");
        Assert.Contains(result.Actions, action => action.Action == "move" && action.Destination!.EffectiveFolderId == "Projects/2026");
        Assert.Contains(result.Actions, action => action.Action == "delete" && action.Succeeded);
        Assert.All(result.Actions, action => Assert.NotNull(action.ConfirmationToken));
    }

    [Fact]
    public async Task MailActionsBundlePreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_actions_bundle_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(8, result.IncludedActionCount);
        Assert.Equal(8, result.SucceededActionCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Contains(result.Actions, action => action.Action == "mark-read" && action.DesiredState == true);
        Assert.Contains(result.Actions, action => action.Action == "mark-unread" && action.DesiredState == false);
        Assert.Contains(result.Actions, action => action.Action == "flag" && action.DesiredState == true);
        Assert.Contains(result.Actions, action => action.Action == "unflag" && action.DesiredState == false);
        Assert.Contains(result.Actions, action => action.Action == "move" && action.Destination!.EffectiveFolderId == "Projects/2026");
    }

    [Fact]
    public async Task MailActionPlanUsesSharedPlanningService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_plan(
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("Move", result.ExecutionKind);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Equal("Projects/2026", result.RequestedDestinationFolderId);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailActionPlanExportUsesSharedPlanExchangeService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_plan_export(
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            path: @"C:\Temp\plan.json",
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(@"C:\Temp\plan.json", fixture.PlanExchangeService.LastSavedPath);
        Assert.NotNull(fixture.PlanExchangeService.LastSavedPlan);
        Assert.Equal("move", fixture.PlanExchangeService.LastSavedPlan!.Action);
    }

    [Fact]
    public async Task MailActionPlanImportUsesSharedPlanExchangeService() {
        using var fixture = new TestFixture();
        fixture.PlanExchangeService.NextPlan = new MessageActionExecutionPlan {
            Succeeded = true,
            Action = "delete",
            ExecutionKind = "Delete",
            ProfileId = "gmail-work",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            MessageIds = { "message-1" }
        };

        var result = await fixture.Tools.mail_action_plan_import(@"C:\Temp\plan.json");

        Assert.Equal("delete", result.Action);
        Assert.Equal(@"C:\Temp\plan.json", fixture.PlanExchangeService.LastLoadedPath);
    }

    [Fact]
    public async Task MailActionBatchImportUsesSharedPlanExchangeService() {
        using var fixture = new TestFixture();
        fixture.PlanExchangeService.NextBatchPlans = new[] {
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "mark-read",
                ExecutionKind = "SetReadState",
                ProfileId = "gmail-work",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                DesiredState = true,
                MessageIds = { "message-1" }
            }
        };

        var result = await fixture.Tools.mail_action_batch_import(@"C:\Temp\plans.json");

        Assert.Single(result);
        Assert.Equal("mark-read", result[0].Action);
        Assert.Equal(@"C:\Temp\plans.json", fixture.PlanExchangeService.LastLoadedBatchPath);
    }

    [Fact]
    public async Task MailActionBatchStoreListUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list();

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.Equal(new[] { "Delete spam" }, batch.PlanNames);
        Assert.Equal(1, fixture.PlanRegistryService.ListCompactCalls);
    }

    [Fact]
    public async Task MailActionBatchStoreSummaryListUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_summary_list();

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.Equal(1, batch.ActionCounts["delete"]);
        Assert.Equal(new[] { "gmail-work" }, batch.ProfileIds);
        Assert.Equal(1, fixture.PlanRegistryService.ListSummaryCalls);
    }

    [Fact]
    public async Task MailActionBatchStoreSummaryListPassesSortToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_summary_list(sortBy: "plans", descending: true);

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(MailMessageActionPlanBatchSortBy.PlanCount, fixture.PlanRegistryService.LastBatchQuery!.SortBy);
        Assert.True(fixture.PlanRegistryService.LastBatchQuery.Descending);
    }

    [Fact]
    public async Task MailActionBatchStoreSummaryListPassesExplicitIdSortToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_summary_list(sortBy: "id");

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(MailMessageActionPlanBatchSortBy.Id, fixture.PlanRegistryService.LastBatchQuery!.SortBy);
        Assert.False(fixture.PlanRegistryService.LastBatchQuery.Descending);
    }

    [Fact]
    public async Task MailActionBatchStoreListPassesPlanNameFilterToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list(new[] { "Delete spam" });

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "Delete spam" }, fixture.PlanRegistryService.LastBatchQuery!.PlanNames);
    }

    [Fact]
    public async Task MailActionBatchStoreListPassesProfileFilterToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list(profileIds: new[] { "gmail-work" });

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "gmail-work" }, fixture.PlanRegistryService.LastBatchQuery!.ProfileIds);
    }

    [Fact]
    public async Task MailActionBatchStoreListPassesActionFilterToSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_compact_list(actions: new[] { "delete" });

        var batch = Assert.Single(result);
        Assert.Equal("cleanup", batch.Id);
        Assert.NotNull(fixture.PlanRegistryService.LastBatchQuery);
        Assert.Equal(new[] { "delete" }, fixture.PlanRegistryService.LastBatchQuery!.Actions);
    }

    [Fact]
    public async Task MailActionBatchStoreImportUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_import("cleanup", "Cleanup batch", @"C:\Temp\plans.json", "Quarterly cleanup");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastImportedBatchId);
        Assert.Equal(@"C:\Temp\plans.json", fixture.PlanRegistryService.LastImportedPath);
    }

    [Fact]
    public async Task MailActionBatchStoreCreateCommonUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_create_common(
            batchId: "cleanup",
            name: "Cleanup batch",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            actions: new[] { "archive", "delete" },
            destinationFolderId: "Archive",
            mailboxId: "primary",
            folderId: "Inbox",
            description: "Common action batch");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastCreatedCommonBatchId);
        Assert.Equal("Cleanup batch", fixture.PlanRegistryService.LastCreatedCommonName);
        Assert.Equal("Common action batch", fixture.PlanRegistryService.LastCreatedCommonDescription);
        Assert.Equal(new[] { "archive", "delete" }, fixture.PlanRegistryService.LastCreatedCommonActions);
        Assert.NotNull(fixture.PlanRegistryService.LastCreatedCommonRequest);
        Assert.Equal("gmail-work", fixture.PlanRegistryService.LastCreatedCommonRequest!.ProfileId);
        Assert.Equal("primary", fixture.PlanRegistryService.LastCreatedCommonRequest.MailboxId);
        Assert.Equal("Inbox", fixture.PlanRegistryService.LastCreatedCommonRequest.FolderId);
        Assert.Equal("Archive", fixture.PlanRegistryService.LastCreatedCommonRequest.DestinationFolderId);
        Assert.Equal(new[] { "message-1", "MESSAGE-1" }, fixture.PlanRegistryService.LastCreatedCommonRequest.MessageIds);
    }

    [Fact]
    public async Task MailActionBatchStoreCreateFromPreviewUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var preview = await fixture.Tools.mail_actions_bundle_preview(
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");
        var result = await fixture.Tools.mail_action_batch_store_create_from_preview(
            batchId: "cleanup-previewed",
            name: "Cleanup Previewed",
            preview: preview,
            actions: new[] { "move", "delete" },
            description: "Built from preview");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup-previewed", fixture.PlanRegistryService.LastCreatedFromPreviewBatchId);
        Assert.Equal("Cleanup Previewed", fixture.PlanRegistryService.LastCreatedFromPreviewName);
        Assert.Equal("Built from preview", fixture.PlanRegistryService.LastCreatedFromPreviewDescription);
        Assert.Equal(new[] { "move", "delete" }, fixture.PlanRegistryService.LastCreatedFromPreviewActions);
        Assert.NotNull(fixture.PlanRegistryService.LastCreatedFromPreview);
        Assert.Equal("gmail-work", fixture.PlanRegistryService.LastCreatedFromPreview!.ProfileId);
        Assert.Equal("primary", fixture.PlanRegistryService.LastCreatedFromPreview.MailboxId);
        Assert.Equal("Inbox", fixture.PlanRegistryService.LastCreatedFromPreview.FolderId);
        Assert.Equal("Projects/2026", fixture.PlanRegistryService.LastCreatedFromPreview.RequestedDestinationFolderId);
        Assert.Equal(new[] { "message-1", "MESSAGE-1" }, fixture.PlanRegistryService.LastCreatedFromPreview.MessageIds);
        Assert.Contains(fixture.PlanRegistryService.LastCreatedFromPreview.Actions, action => action.Action == "move");
    }

    [Fact]
    public async Task MailActionBatchStoreExecuteUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_execute("cleanup");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastExecutedBatchId);
        Assert.Equal(1, result.SucceededPlanCount);
    }

    [Fact]
    public async Task MailActionBatchStoreAppendPlanUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_append_plan(
            batchId: "cleanup",
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1" },
            destinationFolderId: "Archive",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastAppendedBatchId);
        Assert.NotNull(fixture.PlanRegistryService.LastAppendedPlan);
        Assert.Equal("move", fixture.PlanRegistryService.LastAppendedPlan!.Action);
    }

    [Fact]
    public async Task MailActionBatchStoreRemovePlanUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_remove_plan("cleanup", 1);

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastRemovedBatchId);
        Assert.Equal(1, fixture.PlanRegistryService.LastRemovedIndex);
    }

    [Fact]
    public async Task MailActionBatchStoreCloneUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_clone("cleanup", "cleanup-copy", "Cleanup copy", "Cloned batch");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastClonedSourceBatchId);
        Assert.Equal("cleanup-copy", fixture.PlanRegistryService.LastClonedTargetBatchId);
    }

    [Fact]
    public async Task MailActionBatchStoreTransformCloneUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_transform_clone(
            sourceBatchId: "cleanup",
            targetBatchId: "cleanup-target",
            name: "Cleanup Target",
            indexes: new[] { 1, 2 },
            planNames: new[] { "Archive newsletter" },
            profileId: "gmail-target",
            mailboxId: "shared@example.com",
            folderId: "Projects",
            destinationFolderId: "Projects/Archive",
            description: "Remapped batch");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastTransformedSourceBatchId);
        Assert.Equal("cleanup-target", fixture.PlanRegistryService.LastTransformedTargetBatchId);
        Assert.Equal("Cleanup Target", fixture.PlanRegistryService.LastTransformedName);
        Assert.Equal("Remapped batch", fixture.PlanRegistryService.LastTransformedDescription);
        Assert.NotNull(fixture.PlanRegistryService.LastTransformRequest);
        Assert.Equal("gmail-target", fixture.PlanRegistryService.LastTransformRequest!.ProfileId);
        Assert.Equal(new[] { 1, 2 }, fixture.PlanRegistryService.LastTransformRequest.PlanIndexes);
        Assert.Equal(new[] { "Archive newsletter" }, fixture.PlanRegistryService.LastTransformRequest.PlanNames);
        Assert.Equal("shared@example.com", fixture.PlanRegistryService.LastTransformRequest.MailboxId);
        Assert.Equal("Projects", fixture.PlanRegistryService.LastTransformRequest.FolderId);
        Assert.Equal("Projects/Archive", fixture.PlanRegistryService.LastTransformRequest.DestinationFolderId);
    }

    [Fact]
    public async Task MailActionBatchStoreTransformPreviewUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_transform_preview(
            sourceBatchId: "cleanup",
            indexes: new[] { 1 },
            planNames: new[] { "Archive newsletter" },
            profileId: "gmail-target",
            mailboxId: "shared@example.com",
            folderId: "Projects",
            destinationFolderId: "Projects/Archive");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastPreviewedTransformSourceBatchId);
        Assert.NotNull(fixture.PlanRegistryService.LastPreviewedTransformRequest);
        Assert.Equal("gmail-target", fixture.PlanRegistryService.LastPreviewedTransformRequest!.ProfileId);
        Assert.Equal(new[] { 1 }, fixture.PlanRegistryService.LastPreviewedTransformRequest.PlanIndexes);
        Assert.Equal(new[] { "Archive newsletter" }, fixture.PlanRegistryService.LastPreviewedTransformRequest.PlanNames);
        Assert.Equal("shared@example.com", fixture.PlanRegistryService.LastPreviewedTransformRequest.MailboxId);
        Assert.Equal("Projects", fixture.PlanRegistryService.LastPreviewedTransformRequest.FolderId);
        Assert.Equal("Projects/Archive", fixture.PlanRegistryService.LastPreviewedTransformRequest.DestinationFolderId);
        Assert.Equal(1, result.ChangedPlanCount);
    }

    [Fact]
    public async Task MailActionBatchStoreReplacePlanUsesSharedRegistryService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_store_replace_plan(
            batchId: "cleanup",
            index: 0,
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1" },
            destinationFolderId: "Archive",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("cleanup", fixture.PlanRegistryService.LastReplacedBatchId);
        Assert.Equal(0, fixture.PlanRegistryService.LastReplacedIndex);
        Assert.NotNull(fixture.PlanRegistryService.LastReplacedPlan);
        Assert.Equal("move", fixture.PlanRegistryService.LastReplacedPlan!.Action);
    }

    [Fact]
    public async Task MailActionExecuteUsesSharedBatchService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_execute(
            action: "move",
            profileId: "gmail-work",
            messageIds: new[] { "message-1", "MESSAGE-1" },
            destinationFolderId: "Projects/2026",
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RequestedPlanCount);
        Assert.Equal(1, result.SucceededPlanCount);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest!.MailboxId);
        Assert.Equal("Projects/2026", fixture.MessageActionService.LastMoveRequest.DestinationFolderId);
    }

    [Fact]
    public async Task MailActionBatchExecuteUsesSharedBatchService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_action_batch_execute(new[] {
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "mark-read",
                ExecutionKind = "SetReadState",
                ProfileId = "gmail-work",
                MailboxId = "primary",
                FolderId = "Inbox",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                DesiredState = true,
                MessageIds = { "message-1" }
            },
            new MessageActionExecutionPlan {
                Succeeded = true,
                Action = "move",
                ExecutionKind = "Move",
                ProfileId = "gmail-work",
                MailboxId = "primary",
                FolderId = "Inbox",
                RequestedCount = 1,
                UniqueMessageCount = 1,
                RequestedDestinationFolderId = "Projects/2026",
                MessageIds = { "message-2" }
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.AttemptedPlanCount);
        Assert.Equal(2, result.SucceededPlanCount);
        Assert.NotNull(fixture.MessageActionService.LastSetReadStateRequest);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("Projects/2026", fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
    }

    [Fact]
    public async Task MailDeletePreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_delete_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.Equal("gmail-work", result.ProfileId);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailSearchCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_search_compact(
            "gmail-work",
            mailboxId: "primary",
            folderId: "Inbox",
            queryText: "invoice",
            subjectContains: "Quarterly",
            fromContains: "billing@example.com",
            toContains: "team@example.com",
            hasAttachments: true,
            limit: 5);

        var result = Assert.Single(results);
        Assert.Equal("message-1", result.Id);
        Assert.NotNull(fixture.ReadService.LastSearchCompactRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSearchCompactRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSearchCompactRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSearchCompactRequest.FolderId);
        Assert.Equal("invoice", fixture.ReadService.LastSearchCompactRequest.QueryText);
        Assert.True(fixture.ReadService.LastSearchCompactRequest.HasAttachments);
        Assert.Equal(5, fixture.ReadService.LastSearchCompactRequest.Limit);
    }

    [Fact]
    public async Task MailAttachmentsListDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_attachments_list(
            "gmail-work",
            "message-1",
            mailboxId: "primary",
            folderId: "Inbox");

        var result = Assert.Single(results);
        Assert.Equal("attachment-1", result.Id);
        Assert.NotNull(fixture.ReadService.LastListAttachmentsRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastListAttachmentsRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastListAttachmentsRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastListAttachmentsRequest.FolderId);
        Assert.Equal("message-1", fixture.ReadService.LastListAttachmentsRequest.MessageId);
    }

    [Fact]
    public async Task MailAttachmentsSaveDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_attachments_save(
            "gmail-work",
            "message-1",
            @"C:\Temp",
            mailboxId: "primary",
            folderId: "Inbox",
            attachmentIds: new[] { "attachment-1" },
            fileNameContains: "invoice",
            contentTypeContains: "pdf",
            overwrite: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentsRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSaveAttachmentsRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSaveAttachmentsRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSaveAttachmentsRequest.FolderId);
        Assert.Equal(@"C:\Temp", fixture.ReadService.LastSaveAttachmentsRequest.DestinationPath);
        Assert.Contains("attachment-1", fixture.ReadService.LastSaveAttachmentsRequest.AttachmentIds);
        Assert.Equal("invoice", fixture.ReadService.LastSaveAttachmentsRequest.FileNameContains);
        Assert.Equal("pdf", fixture.ReadService.LastSaveAttachmentsRequest.ContentTypeContains);
        Assert.True(fixture.ReadService.LastSaveAttachmentsRequest.Overwrite);
    }

    [Fact]
    public async Task MailAttachmentsSaveManyDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_attachments_save_many(
            "gmail-work",
            new[] { "message-1", "message-2" },
            @"C:\Temp",
            mailboxId: "primary",
            folderId: "Inbox",
            attachmentIds: new[] { "attachment-1" },
            fileNameContains: "invoice",
            contentTypeContains: "pdf",
            overwrite: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentsManyRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastSaveAttachmentsManyRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastSaveAttachmentsManyRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastSaveAttachmentsManyRequest.FolderId);
        Assert.Equal(@"C:\Temp", fixture.ReadService.LastSaveAttachmentsManyRequest.DestinationPath);
        Assert.Equal(new[] { "message-1", "message-2" }, fixture.ReadService.LastSaveAttachmentsManyRequest.MessageIds);
        Assert.Equal(new[] { "attachment-1" }, fixture.ReadService.LastSaveAttachmentsManyRequest.AttachmentIds);
        Assert.Equal("invoice", fixture.ReadService.LastSaveAttachmentsManyRequest.FileNameContains);
        Assert.Equal("pdf", fixture.ReadService.LastSaveAttachmentsManyRequest.ContentTypeContains);
        Assert.True(fixture.ReadService.LastSaveAttachmentsManyRequest.Overwrite);
    }

    [Fact]
    public async Task MailGetCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_get_compact(
            "gmail-work",
            "message-1",
            mailboxId: "primary",
            folderId: "Inbox",
            includeRawContent: true);

        Assert.Equal("message-1", result.Id);
        Assert.NotNull(fixture.ReadService.LastGetCompactRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastGetCompactRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastGetCompactRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastGetCompactRequest.FolderId);
        Assert.Equal("message-1", fixture.ReadService.LastGetCompactRequest.MessageId);
        Assert.True(fixture.ReadService.LastGetCompactRequest.IncludeRawContent);
    }

    [Fact]
    public async Task MailGetManyCompactDelegatesToApplicationReadService() {
        using var fixture = new TestFixture();

        var results = await fixture.Tools.mail_get_many_compact(
            "gmail-work",
            new[] { "message-1", "message-2" },
            mailboxId: "primary",
            folderId: "Inbox",
            includeRawContent: true);

        Assert.Equal(2, results.Count);
        Assert.NotNull(fixture.ReadService.LastGetManyCompactRequest);
        Assert.Equal("gmail-work", fixture.ReadService.LastGetManyCompactRequest!.ProfileId);
        Assert.Equal("primary", fixture.ReadService.LastGetManyCompactRequest.MailboxId);
        Assert.Equal("Inbox", fixture.ReadService.LastGetManyCompactRequest.FolderId);
        Assert.Equal(new[] { "message-1", "message-2" }, fixture.ReadService.LastGetManyCompactRequest.MessageIds);
        Assert.True(fixture.ReadService.LastGetManyCompactRequest.IncludeRawContent);
    }

    [Fact]
    public async Task MailMarkReadDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_mark";

        var result = await fixture.Tools.mail_mark_read(
            "gmail-work",
            new[] { "message-1", "message-2" },
            isRead: false,
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastSetReadStateRequest);
        Assert.Equal("gmail-work", fixture.MessageActionService.LastSetReadStateRequest!.ProfileId);
        Assert.Equal("primary", fixture.MessageActionService.LastSetReadStateRequest.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionService.LastSetReadStateRequest.FolderId);
        Assert.False(fixture.MessageActionService.LastSetReadStateRequest.IsRead);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastSetReadStateRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailFlagDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_flag";

        var result = await fixture.Tools.mail_flag(
            "gmail-work",
            new[] { "message-1", "message-2" },
            isFlagged: false,
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastSetFlaggedStateRequest);
        Assert.Equal("gmail-work", fixture.MessageActionService.LastSetFlaggedStateRequest!.ProfileId);
        Assert.Equal("primary", fixture.MessageActionService.LastSetFlaggedStateRequest.MailboxId);
        Assert.Equal("Inbox", fixture.MessageActionService.LastSetFlaggedStateRequest.FolderId);
        Assert.False(fixture.MessageActionService.LastSetFlaggedStateRequest.IsFlagged);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastSetFlaggedStateRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailMarkReadPreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_mark_read_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            isRead: false,
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("read-state", result.Action);
        Assert.False(result.DesiredState);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailFlagPreviewUsesSharedPreviewService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_flag_preview(
            "gmail-work",
            new[] { "message-1", "MESSAGE-1" },
            isFlagged: false,
            mailboxId: "primary",
            folderId: "Inbox");

        Assert.True(result.Succeeded);
        Assert.Equal("flagged-state", result.Action);
        Assert.False(result.DesiredState);
        Assert.Equal(2, result.UniqueMessageCount);
        Assert.NotNull(result.ConfirmationToken);
    }

    [Fact]
    public async Task MailArchiveDelegatesToSharedArchiveAlias() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_archive";

        var result = await fixture.Tools.mail_archive(
            "gmail-work",
            new[] { "message-1" },
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal(MailFolderAliases.Archive, fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailTrashDelegatesToSharedTrashAlias() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_trash";

        var result = await fixture.Tools.mail_trash(
            "gmail-work",
            new[] { "message-1" },
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal(MailFolderAliases.Trash, fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailMoveDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_move";

        var result = await fixture.Tools.mail_move(
            "gmail-work",
            new[] { "message-1" },
            "Archive",
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastMoveRequest);
        Assert.Equal("Archive", fixture.MessageActionService.LastMoveRequest!.DestinationFolderId);
        Assert.Equal("primary", fixture.MessageActionService.LastMoveRequest.MailboxId);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastMoveRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailDeleteDelegatesToApplicationMessageActionService() {
        using var fixture = new TestFixture();
        const string confirmationToken = "mact_v1_delete";

        var result = await fixture.Tools.mail_delete(
            "gmail-work",
            new[] { "message-1", "message-2" },
            mailboxId: "primary",
            folderId: "Inbox",
            confirmationToken: confirmationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.MessageActionService.LastDeleteRequest);
        Assert.Equal("gmail-work", fixture.MessageActionService.LastDeleteRequest!.ProfileId);
        Assert.Equal(new[] { "message-1", "message-2" }, fixture.MessageActionService.LastDeleteRequest.MessageIds);
        Assert.Equal(confirmationToken, fixture.MessageActionService.LastDeleteRequest.ConfirmationToken);
    }

    [Fact]
    public async Task MailSendBuildsQueueFirstSendRequest() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_send(
            "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Status update",
            textBody: "Queued body",
            cc: new[] { "bob@example.com" },
            attachmentPaths: new[] { "C:\\Temp\\status.txt" });

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("gmail-work", fixture.SendService.LastRequest!.ProfileId);
        Assert.True(fixture.SendService.LastRequest.PreferQueue);
        Assert.False(fixture.SendService.LastRequest.RequireImmediateSend);
        Assert.Equal("alice@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("bob@example.com", fixture.SendService.LastRequest.Message.Cc[0].Address);
        Assert.Equal("C:\\Temp\\status.txt", fixture.SendService.LastRequest.Message.Attachments[0].Path);
    }

    [Fact]
    public async Task MailDraftSaveAndListRoundTripsThroughDraftService() {
        using var fixture = new TestFixture();

        var saveResult = await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update",
            textBody: "Draft body",
            cc: new[] { "bob@example.com" });

        Assert.True(saveResult.Succeeded);

        var drafts = await fixture.Tools.mail_draft_list();

        var draft = Assert.Single(drafts);
        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("Weekly update", draft.Name);
        Assert.Equal("gmail-work", draft.Message.ProfileId);
        Assert.Equal("alice@example.com", draft.Message.To[0].Address);
        Assert.Equal("bob@example.com", draft.Message.Cc[0].Address);
    }

    [Fact]
    public async Task MailDraftCompactListReturnsLightweightProjection() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var drafts = await fixture.Tools.mail_draft_compact_list();

        var draft = Assert.Single(drafts);
        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("gmail-work", draft.ProfileId);
        Assert.Equal("Weekly update", draft.Subject);
    }

    [Fact]
    public async Task MailDraftGetReturnsStoredDraft() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var draft = await fixture.Tools.mail_draft_get("draft-1");

        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("Weekly update", draft.Name);
        Assert.Equal("alice@example.com", draft.Message.To[0].Address);
    }

    [Fact]
    public async Task MailDraftCompactGetReturnsLightweightProjection() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var draft = await fixture.Tools.mail_draft_compact_get("draft-1");

        Assert.Equal("draft-1", draft.Id);
        Assert.Equal("gmail-work", draft.ProfileId);
        Assert.Equal("Weekly update", draft.Subject);
    }

    [Fact]
    public async Task MailDraftSendUsesStoredDraft() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update",
            textBody: "Draft body");

        var result = await fixture.Tools.mail_draft_send("draft-1", sendNow: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("gmail-work", fixture.SendService.LastRequest!.ProfileId);
        Assert.True(fixture.SendService.LastRequest.RequireImmediateSend);
        Assert.False(fixture.SendService.LastRequest.PreferQueue);
        Assert.Equal("alice@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("Weekly update", fixture.SendService.LastRequest.Message.Subject);
    }

    [Fact]
    public async Task MailDraftImportLoadsDraftFileIntoSharedStore() {
        using var fixture = new TestFixture();
        var path = fixture.CreatePath("imported-draft.json");
        await fixture.Application.DraftExchange.SaveAsync(path, new MailDraft {
            Id = "external-draft",
            Name = "Imported draft",
            Message = new DraftMessage {
                ProfileId = "gmail-work",
                Subject = "Imported subject",
                To = {
                    new MessageRecipient { Address = "imported@example.com" }
                }
            }
        });

        var imported = await fixture.Tools.mail_draft_import(path, draftId: "draft-1", name: "Imported into store");
        var stored = await fixture.Tools.mail_draft_get("draft-1");

        Assert.Equal("draft-1", imported.Id);
        Assert.Equal("Imported into store", imported.Name);
        Assert.Equal("draft-1", stored.Id);
        Assert.Equal("Imported into store", stored.Name);
        Assert.Equal("imported@example.com", stored.Message.To[0].Address);
    }

    [Fact]
    public async Task MailDraftExportWritesStoredDraftFile() {
        using var fixture = new TestFixture();
        var path = fixture.CreatePath("exported-draft.json");
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var result = await fixture.Tools.mail_draft_export("draft-1", path);
        var exported = await fixture.Application.DraftExchange.LoadAsync(path);

        Assert.True(result.Succeeded);
        Assert.Equal("draft-1", exported.Id);
        Assert.Equal("Weekly update", exported.Name);
        Assert.Equal("alice@example.com", exported.Message.To[0].Address);
    }

    [Fact]
    public async Task MailDraftDeleteRemovesStoredDraft() {
        using var fixture = new TestFixture();
        await fixture.Tools.mail_draft_save(
            draftId: "draft-1",
            name: "Weekly update",
            profileId: "gmail-work",
            to: new[] { "alice@example.com" },
            subject: "Weekly update");

        var deleteResult = await fixture.Tools.mail_draft_delete("draft-1");
        var drafts = await fixture.Tools.mail_draft_list();

        Assert.True(deleteResult.Succeeded);
        Assert.Empty(drafts);
    }

    [Fact]
    public async Task MailQueueProcessDelegatesToQueueService() {
        using var fixture = new TestFixture();

        var result = await fixture.Tools.mail_queue_process();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(1, result.SentCount);
        Assert.True(fixture.QueueService.ProcessCalled);
    }

    [Fact]
    public async Task MailQueueCompactListReturnsLightweightProjection() {
        using var fixture = new TestFixture();

        var queued = await fixture.Tools.mail_queue_compact_list();

        var message = Assert.Single(queued);
        Assert.Equal("queued-1", message.MessageId);
        Assert.Equal("gmail", message.Provider);
    }

    [Fact]
    public async Task MailQueueCompactGetReturnsLightweightProjection() {
        using var fixture = new TestFixture();

        var message = await fixture.Tools.mail_queue_compact_get("queued-1");

        Assert.Equal("queued-1", message.MessageId);
        Assert.Equal("gmail", message.Provider);
    }

    private sealed class TestFixture : IDisposable {
        private readonly string _tempDirectory;

        public TestFixture() {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "Mailozaurr.McpTests", Guid.NewGuid().ToString("N"));

            ProfileStore = new InMemoryProfileStore(new[] {
                new MailProfile {
                    Id = "gmail-work",
                    DisplayName = "Work Gmail",
                    Kind = MailProfileKind.Gmail
                }
            });
            SecretStore = new InMemorySecretStore();
            ReadService = new FakeReadService();
            SendService = new FakeSendService();
            MessageActionService = new FakeMessageActionService();
            PlanExchangeService = new FakeMessageActionPlanExchangeService();
            PlanRegistryService = new FakeMessageActionPlanRegistryService();
            QueueService = new FakeQueueService();
            ProfileAuthService = new FakeProfileAuthService(ProfileStore, SecretStore);
            ProfileConnectionService = new FakeProfileConnectionService();

            Application = new MailApplicationBuilder()
                .UseProfileStore(ProfileStore)
                .UseSecretStore(SecretStore)
                .UseDraftStore(new FileMailDraftStore(Path.Combine(_tempDirectory, "drafts.json")))
                .UseProfileAuthService(ProfileAuthService)
                .UseProfileConnectionService(ProfileConnectionService)
                .UseReadService(ReadService)
                .UseMessageActionService(MessageActionService)
                .UseMessageActionPlanExchangeService(PlanExchangeService)
                .UseMessageActionPlanRegistryService(PlanRegistryService)
                .UseSendService(SendService)
                .UseQueueService(QueueService)
                .Build();

            Tools = new MailMcpTools(Application);
        }

        public MailApplication Application { get; }

        public MailMcpTools Tools { get; }

        public InMemoryProfileStore ProfileStore { get; }

        public InMemorySecretStore SecretStore { get; }

        public FakeReadService ReadService { get; }

        public FakeSendService SendService { get; }

        public FakeMessageActionService MessageActionService { get; }

        public FakeMessageActionPlanExchangeService PlanExchangeService { get; }

        public FakeMessageActionPlanRegistryService PlanRegistryService { get; }

        public FakeQueueService QueueService { get; }

        public FakeProfileAuthService ProfileAuthService { get; }

        public FakeProfileConnectionService ProfileConnectionService { get; }

        public string CreatePath(string fileName) => Path.Combine(_tempDirectory, fileName);

        public void Dispose() {
            if (Directory.Exists(_tempDirectory)) {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeMessageActionPlanExchangeService : IMailMessageActionPlanExchangeService {
        public string? LastLoadedPath { get; private set; }

        public string? LastLoadedBatchPath { get; private set; }

        public string? LastSavedPath { get; private set; }

        public string? LastSavedBatchPath { get; private set; }

        public MessageActionExecutionPlan? LastSavedPlan { get; private set; }

        public IReadOnlyList<MessageActionExecutionPlan>? LastSavedBatch { get; private set; }

        public MessageActionExecutionPlan NextPlan { get; set; } = new() {
            Succeeded = true,
            Action = "move",
            ExecutionKind = "Move",
            ProfileId = "gmail-work",
            RequestedCount = 1,
            UniqueMessageCount = 1,
            RequestedDestinationFolderId = "Archive",
            MessageIds = { "message-1" }
        };

        public IReadOnlyList<MessageActionExecutionPlan> NextBatchPlans { get; set; } = Array.Empty<MessageActionExecutionPlan>();

        public Task<MessageActionExecutionPlan> LoadAsync(string path, CancellationToken cancellationToken = default) {
            LastLoadedPath = path;
            return Task.FromResult(NextPlan);
        }

        public Task<IReadOnlyList<MessageActionExecutionPlan>> LoadBatchAsync(string path, CancellationToken cancellationToken = default) {
            LastLoadedBatchPath = path;
            return Task.FromResult(NextBatchPlans);
        }

        public Task SaveAsync(string path, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
            LastSavedPath = path;
            LastSavedPlan = plan;
            return Task.CompletedTask;
        }

        public Task SaveBatchAsync(string path, IReadOnlyList<MessageActionExecutionPlan> plans, CancellationToken cancellationToken = default) {
            LastSavedBatchPath = path;
            LastSavedBatch = plans;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMessageActionPlanRegistryService : IMailMessageActionPlanRegistryService {
        public int ListCompactCalls { get; private set; }
        public int ListSummaryCalls { get; private set; }
        public MailMessageActionPlanBatchQuery? LastBatchQuery { get; private set; }

        public string? LastAppendedBatchId { get; private set; }

        public MessageActionExecutionPlan? LastAppendedPlan { get; private set; }

        public string? LastClonedSourceBatchId { get; private set; }

        public string? LastClonedTargetBatchId { get; private set; }

        public string? LastImportedBatchId { get; private set; }

        public string? LastImportedPath { get; private set; }

        public string? LastCreatedCommonBatchId { get; private set; }

        public string? LastCreatedCommonName { get; private set; }

        public string? LastCreatedCommonDescription { get; private set; }

        public CommonMessageActionsPreviewRequest? LastCreatedCommonRequest { get; private set; }

        public IReadOnlyList<string>? LastCreatedCommonActions { get; private set; }

        public string? LastCreatedFromPreviewBatchId { get; private set; }

        public string? LastCreatedFromPreviewName { get; private set; }

        public string? LastCreatedFromPreviewDescription { get; private set; }

        public CommonMessageActionsPreview? LastCreatedFromPreview { get; private set; }

        public IReadOnlyList<string>? LastCreatedFromPreviewActions { get; private set; }

        public string? LastTransformedSourceBatchId { get; private set; }

        public string? LastTransformedTargetBatchId { get; private set; }

        public string? LastTransformedName { get; private set; }

        public string? LastTransformedDescription { get; private set; }

        public MessageActionPlanBatchTransformRequest? LastTransformRequest { get; private set; }

        public string? LastPreviewedTransformSourceBatchId { get; private set; }

        public MessageActionPlanBatchTransformRequest? LastPreviewedTransformRequest { get; private set; }

        public string? LastExecutedBatchId { get; private set; }

        public string? LastRemovedBatchId { get; private set; }

        public int? LastRemovedIndex { get; private set; }

        public string? LastReplacedBatchId { get; private set; }

        public int? LastReplacedIndex { get; private set; }

        public MessageActionExecutionPlan? LastReplacedPlan { get; private set; }

        public Task<OperationResult> AppendImportedPlanAsync(string batchId, string path, CancellationToken cancellationToken = default) {
            LastAppendedBatchId = batchId;
            LastImportedPath = path;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> AppendPlanAsync(string batchId, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
            LastAppendedBatchId = batchId;
            LastAppendedPlan = plan;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> CloneAsync(string sourceBatchId, string targetBatchId, string name, string? description = null, CancellationToken cancellationToken = default) {
            LastClonedSourceBatchId = sourceBatchId;
            LastClonedTargetBatchId = targetBatchId;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{targetBatchId}' saved."));
        }

        public Task<OperationResult> CreateCommonBatchAsync(
            string batchId,
            string name,
            CommonMessageActionsPreviewRequest request,
            IReadOnlyList<string>? actions = null,
            string? description = null,
            CancellationToken cancellationToken = default) {
            LastCreatedCommonBatchId = batchId;
            LastCreatedCommonName = name;
            LastCreatedCommonDescription = description;
            LastCreatedCommonRequest = new CommonMessageActionsPreviewRequest {
                ProfileId = request.ProfileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                DestinationFolderId = request.DestinationFolderId,
                MessageIds = request.MessageIds.ToList()
            };
            LastCreatedCommonActions = actions?.ToArray();
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> CreateCommonBatchFromPreviewAsync(
            string batchId,
            string name,
            CommonMessageActionsPreview preview,
            IReadOnlyList<string>? actions = null,
            string? description = null,
            CancellationToken cancellationToken = default) {
            LastCreatedFromPreviewBatchId = batchId;
            LastCreatedFromPreviewName = name;
            LastCreatedFromPreviewDescription = description;
            LastCreatedFromPreview = new CommonMessageActionsPreview {
                ProfileId = preview.ProfileId,
                MailboxId = preview.MailboxId,
                FolderId = preview.FolderId,
                RequestedDestinationFolderId = preview.RequestedDestinationFolderId,
                RequestedCount = preview.RequestedCount,
                UniqueMessageCount = preview.UniqueMessageCount,
                DuplicateOrEmptyCount = preview.DuplicateOrEmptyCount,
                MessageIds = preview.MessageIds.ToList(),
                Actions = preview.Actions.Select(action => new MessageActionPreviewItem {
                    Action = action.Action,
                    DisplayName = action.DisplayName,
                    Succeeded = action.Succeeded,
                    Code = action.Code,
                    Message = action.Message,
                    RequestedDestinationFolderId = action.RequestedDestinationFolderId,
                    DesiredState = action.DesiredState,
                    ConfirmationToken = action.ConfirmationToken
                }).ToList()
            };
            LastCreatedFromPreviewActions = actions?.ToArray();
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> TransformCloneAsync(
            string sourceBatchId,
            string targetBatchId,
            string name,
            MessageActionPlanBatchTransformRequest transform,
            string? description = null,
            CancellationToken cancellationToken = default) {
            LastTransformedSourceBatchId = sourceBatchId;
            LastTransformedTargetBatchId = targetBatchId;
            LastTransformedName = name;
            LastTransformedDescription = description;
            LastTransformRequest = new MessageActionPlanBatchTransformRequest {
                PlanIndexes = transform.PlanIndexes.ToList(),
                PlanNames = transform.PlanNames.ToList(),
                ProfileId = transform.ProfileId,
                MailboxId = transform.MailboxId,
                FolderId = transform.FolderId,
                DestinationFolderId = transform.DestinationFolderId
            };
            return Task.FromResult(OperationResult.Success($"Action plan batch '{targetBatchId}' saved."));
        }

        public Task<MailMessageActionPlanBatchTransformPreview> PreviewTransformCloneAsync(
            string sourceBatchId,
            MessageActionPlanBatchTransformRequest transform,
            CancellationToken cancellationToken = default) {
            LastPreviewedTransformSourceBatchId = sourceBatchId;
            LastPreviewedTransformRequest = new MessageActionPlanBatchTransformRequest {
                PlanIndexes = transform.PlanIndexes.ToList(),
                PlanNames = transform.PlanNames.ToList(),
                ProfileId = transform.ProfileId,
                MailboxId = transform.MailboxId,
                FolderId = transform.FolderId,
                DestinationFolderId = transform.DestinationFolderId
            };
            return Task.FromResult(new MailMessageActionPlanBatchTransformPreview {
                Succeeded = true,
                SourceBatchId = sourceBatchId,
                SourceBatchName = "Cleanup batch",
                PlanCount = 1,
                ChangedPlanCount = 1,
                ConfirmationTokenChangedCount = 1,
                TargetProfileExists = true,
                Plans = {
                    new MessageActionPlanBatchTransformPreviewItem {
                        Index = 0,
                        Action = "move",
                        ExecutionKind = "Move",
                        SourceProfileId = "gmail-work",
                        TargetProfileId = transform.ProfileId ?? "gmail-work",
                        SourceMailboxId = "primary",
                        TargetMailboxId = transform.MailboxId,
                        SourceFolderId = "Inbox",
                        TargetFolderId = transform.FolderId,
                        SourceDestinationFolderId = "Archive",
                        TargetDestinationFolderId = transform.DestinationFolderId,
                        WillChange = true,
                        ConfirmationTokenWillChange = true,
                        Summary = "move: preview"
                    }
                },
                Message = "Previewed transform."
            });
        }

        public Task<OperationResult> DeleteAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' deleted."));

        public Task<MessageActionBatchExecutionResult> ExecuteAsync(string batchId, bool continueOnError = true, CancellationToken cancellationToken = default) {
            LastExecutedBatchId = batchId;
            return Task.FromResult(new MessageActionBatchExecutionResult {
                Succeeded = true,
                RequestedPlanCount = 1,
                AttemptedPlanCount = 1,
                SucceededPlanCount = 1,
                Message = "Stored batch executed."
            });
        }

        public Task<OperationResult> ExportAsync(string batchId, string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' exported."));

        public Task<MailMessageActionPlanBatch?> GetBatchAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MailMessageActionPlanBatch?>(new MailMessageActionPlanBatch {
                Id = batchId,
                Name = "Cleanup batch",
                Plans = {
                    new MessageActionExecutionPlan {
                        Succeeded = true,
                        Action = "delete",
                        ExecutionKind = "Delete",
                        ProfileId = "gmail-work",
                        RequestedCount = 1,
                        UniqueMessageCount = 1,
                        MessageIds = { "message-1" }
                    }
                }
            });

        public Task<MailMessageActionPlanBatchCompact?> GetBatchCompactAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MailMessageActionPlanBatchCompact?>(new MailMessageActionPlanBatchCompact {
                Id = batchId,
                Name = "Cleanup batch",
                PlanCount = 1,
                ReadyPlanCount = 1,
                ProfileCount = 1,
                PlanNames = { "Delete spam" },
                Summary = $"{batchId} (1 plan(s), 1 ready)"
            });

        public Task<MailMessageActionPlanBatchSummary?> GetBatchSummaryAsync(string batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MailMessageActionPlanBatchSummary?>(new MailMessageActionPlanBatchSummary {
                Id = batchId,
                Name = "Cleanup batch",
                PlanCount = 1,
                ReadyPlanCount = 1,
                ProfileIds = { "gmail-work" },
                ActionCounts = {
                    ["delete"] = 1
                },
                PlanNames = { "Delete spam" },
                Summary = $"{batchId} (1 plan(s), 1 ready, 1 action type(s))"
            });

        public Task<IReadOnlyList<MailMessageActionPlanBatch>> GetBatchesAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) {
            LastBatchQuery = query == null
                ? null
                : new MailMessageActionPlanBatchQuery {
                    PlanNames = query.PlanNames.ToList(),
                    ProfileIds = query.ProfileIds.ToList(),
                    Actions = query.Actions.ToList(),
                    SortBy = query.SortBy,
                    Descending = query.Descending
                };
            return Task.FromResult<IReadOnlyList<MailMessageActionPlanBatch>>(new[] {
                new MailMessageActionPlanBatch {
                    Id = "cleanup",
                    Name = "Cleanup batch",
                    Plans = {
                        new MessageActionExecutionPlan {
                            Succeeded = true,
                            Action = "delete",
                            ExecutionKind = "Delete",
                            ProfileId = "gmail-work",
                            RequestedCount = 1,
                            UniqueMessageCount = 1,
                            MessageIds = { "message-1" }
                        }
                    }
                }
            });
        }

        public Task<IReadOnlyList<MailMessageActionPlanBatchCompact>> GetBatchesCompactAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) {
            LastBatchQuery = query == null
                ? null
                : new MailMessageActionPlanBatchQuery {
                    PlanNames = query.PlanNames.ToList(),
                    ProfileIds = query.ProfileIds.ToList(),
                    Actions = query.Actions.ToList(),
                    SortBy = query.SortBy,
                    Descending = query.Descending
                };
            ListCompactCalls++;
            return Task.FromResult<IReadOnlyList<MailMessageActionPlanBatchCompact>>(new[] {
                new MailMessageActionPlanBatchCompact {
                    Id = "cleanup",
                    Name = "Cleanup batch",
                    PlanCount = 1,
                    ReadyPlanCount = 1,
                    ProfileCount = 1,
                    PlanNames = { "Delete spam" },
                    Summary = "cleanup (1 plan(s), 1 ready)"
                }
            });
        }

        public Task<IReadOnlyList<MailMessageActionPlanBatchSummary>> GetBatchesSummaryAsync(MailMessageActionPlanBatchQuery? query = null, CancellationToken cancellationToken = default) {
            LastBatchQuery = query == null
                ? null
                : new MailMessageActionPlanBatchQuery {
                    PlanNames = query.PlanNames.ToList(),
                    ProfileIds = query.ProfileIds.ToList(),
                    Actions = query.Actions.ToList(),
                    SortBy = query.SortBy,
                    Descending = query.Descending
                };
            ListSummaryCalls++;
            return Task.FromResult<IReadOnlyList<MailMessageActionPlanBatchSummary>>(new[] {
                new MailMessageActionPlanBatchSummary {
                    Id = "cleanup",
                    Name = "Cleanup batch",
                    PlanCount = 1,
                    ReadyPlanCount = 1,
                    ProfileIds = { "gmail-work" },
                    ActionCounts = {
                        ["delete"] = 1
                    },
                    PlanNames = { "Delete spam" },
                    Summary = "cleanup (1 plan(s), 1 ready, 1 action type(s))"
                }
            });
        }

        public Task<OperationResult> ImportAsync(string batchId, string name, string path, string? description = null, CancellationToken cancellationToken = default) {
            LastImportedBatchId = batchId;
            LastImportedPath = path;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> ReplaceImportedPlanAtAsync(string batchId, int index, string path, CancellationToken cancellationToken = default) {
            LastReplacedBatchId = batchId;
            LastReplacedIndex = index;
            LastImportedPath = path;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> ReplacePlanAtAsync(string batchId, int index, MessageActionExecutionPlan plan, CancellationToken cancellationToken = default) {
            LastReplacedBatchId = batchId;
            LastReplacedIndex = index;
            LastReplacedPlan = plan;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> RemovePlanAtAsync(string batchId, int index, CancellationToken cancellationToken = default) {
            LastRemovedBatchId = batchId;
            LastRemovedIndex = index;
            return Task.FromResult(OperationResult.Success($"Action plan batch '{batchId}' saved."));
        }

        public Task<OperationResult> SaveAsync(MailMessageActionPlanBatch batch, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success($"Action plan batch '{batch.Id}' saved."));
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

    private sealed class FakeReadService : IMailReadService {
        public MailFolderQuery? LastFolderQuery { get; private set; }

        public GetMessageRequest? LastGetCompactRequest { get; private set; }

        public GetMessagesRequest? LastGetManyCompactRequest { get; private set; }

        public GetMessagesRequest? LastGetManyRequest { get; private set; }

        public SaveAttachmentsManyRequest? LastSaveAttachmentsManyRequest { get; private set; }

        public SaveAttachmentsRequest? LastSaveAttachmentsRequest { get; private set; }

        public ListAttachmentsRequest? LastListAttachmentsRequest { get; private set; }

        public MailFolderQuery? LastFolderCompactQuery { get; private set; }

        public MailSearchRequest? LastSearchCompactRequest { get; private set; }

        public MailSearchRequest? LastSearchRequest { get; private set; }

        public Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
            LastFolderCompactQuery = query;
            return Task.FromResult<IReadOnlyList<FolderRefCompact>>(new[] {
                new FolderRefCompact {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "Inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox",
                    Summary = "Inbox Inbox"
                }
            });
        }

        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
            LastFolderQuery = query;
            return Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "Inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox",
                    SpecialUse = "inbox"
                },
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "archive",
                    DisplayName = "Archive",
                    Path = "Archive",
                    SpecialUse = "archive"
                },
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "trash",
                    DisplayName = "Trash",
                    Path = "Trash",
                    SpecialUse = "trash"
                }
            });
        }

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
            LastSearchRequest = request;
            return Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = "message-1",
                    Subject = "Quarterly invoice"
                }
            });
        }

        public Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
            LastSearchCompactRequest = request;
            return Task.FromResult<IReadOnlyList<MessageSummaryCompact>>(new[] {
                new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = "message-1",
                    Subject = "Quarterly invoice",
                    Summary = "message-1 Quarterly invoice"
                }
            });
        }

        public Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default) {
            LastListAttachmentsRequest = request;
            return Task.FromResult<IReadOnlyList<AttachmentSummary>>(new[] {
                new AttachmentSummary {
                    MessageId = request.MessageId,
                    Id = "attachment-1",
                    FileName = "invoice.pdf",
                    SizeInBytes = 4096
                }
            });
        }

        public Task<SaveAttachmentsResult> SaveAttachmentsAsync(SaveAttachmentsRequest request, CancellationToken cancellationToken = default) {
            LastSaveAttachmentsRequest = request;
            return Task.FromResult(new SaveAttachmentsResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                MessageId = request.MessageId,
                MatchedCount = 1,
                AttemptedCount = 1,
                SavedCount = 1,
                FailedCount = 0,
                Message = "Saved 1 attachment(s).",
                Results = {
                    new SavedAttachmentResult {
                        Succeeded = true,
                        AttachmentId = "attachment-1",
                        FileName = "invoice.pdf",
                        ContentType = "application/pdf"
                    }
                }
            });
        }

        public Task<SaveAttachmentsManyResult> SaveAttachmentsManyAsync(SaveAttachmentsManyRequest request, CancellationToken cancellationToken = default) {
            LastSaveAttachmentsManyRequest = request;
            return Task.FromResult(new SaveAttachmentsManyResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                RequestedMessageCount = request.MessageIds.Count,
                AttemptedMessageCount = request.MessageIds.Count,
                SucceededMessageCount = request.MessageIds.Count,
                MatchedCount = request.MessageIds.Count,
                AttemptedCount = request.MessageIds.Count,
                SavedCount = request.MessageIds.Count,
                FailedCount = 0,
                Message = $"Saved {request.MessageIds.Count} attachment(s) across {request.MessageIds.Count} message(s).",
                MessageResults = request.MessageIds.Select(messageId => new SaveAttachmentsResult {
                    Succeeded = true,
                    ProfileId = request.ProfileId,
                    MessageId = messageId,
                    MatchedCount = 1,
                    AttemptedCount = 1,
                    SavedCount = 1,
                    FailedCount = 0,
                    Message = "Saved 1 attachment(s)."
                }).ToList()
            });
        }

        public Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default) {
            LastGetCompactRequest = request;
            return Task.FromResult<MessageDetailCompact?>(new MessageDetailCompact {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Retrieved message",
                    Summary = $"{request.MessageId} Retrieved message"
                },
                TextBodyPreview = "Preview",
                HtmlBodyPreview = "<p>Preview</p>",
                HasRawContent = request.IncludeRawContent,
                SummaryText = $"{request.MessageId} Retrieved message"
            });
        }

        public Task<IReadOnlyList<MessageDetailCompact>> GetMessagesCompactAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) {
            LastGetManyCompactRequest = request;
            return Task.FromResult<IReadOnlyList<MessageDetailCompact>>(request.MessageIds.Select(messageId => new MessageDetailCompact {
                ProfileId = request.ProfileId,
                Id = messageId,
                Summary = new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = messageId,
                    Subject = "Retrieved message",
                    Summary = $"{messageId} Retrieved message"
                },
                TextBodyPreview = "Preview",
                HtmlBodyPreview = "<p>Preview</p>",
                HasRawContent = request.IncludeRawContent,
                SummaryText = $"{messageId} Retrieved message"
            }).ToArray());
        }

        public Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(new MessageDetail {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Retrieved message"
                }
            });

        public Task<IReadOnlyList<MessageDetail>> GetMessagesAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) {
            LastGetManyRequest = request;
            return Task.FromResult<IReadOnlyList<MessageDetail>>(request.MessageIds.Select(messageId => new MessageDetail {
                ProfileId = request.ProfileId,
                Id = messageId,
                Summary = new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = messageId,
                    Subject = "Retrieved message"
                }
            }).ToArray());
        }

        public Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Attachment saved."));
    }

    private sealed class FakeSendService : IMailSendService {
        public SendMessageRequest? LastRequest { get; private set; }

        public Task<SendResult> SendAsync(SendMessageRequest request, CancellationToken cancellationToken = default) {
            LastRequest = request;
            return Task.FromResult(new SendResult {
                Succeeded = true,
                Queued = request.PreferQueue,
                QueueMessageId = request.PreferQueue ? "queued-1" : null,
                ProviderMessageId = request.RequireImmediateSend ? "provider-1" : null,
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Gmail,
                Message = "Send handled."
            });
        }
    }

    private sealed class FakeMessageActionService : IMailMessageActionService {
        public SetReadStateRequest? LastSetReadStateRequest { get; private set; }

        public SetFlaggedStateRequest? LastSetFlaggedStateRequest { get; private set; }

        public MoveMessagesRequest? LastMoveRequest { get; private set; }

        public DeleteMessagesRequest? LastDeleteRequest { get; private set; }

        public Task<MessageActionResult> SetReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default) {
            LastSetReadStateRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, request.IsRead ? "Marked messages as read." : "Marked messages as unread."));
        }

        public Task<MessageActionResult> SetFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default) {
            LastSetFlaggedStateRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, request.IsFlagged ? "Flagged messages." : "Unflagged messages."));
        }

        public Task<MessageActionResult> MoveAsync(MoveMessagesRequest request, CancellationToken cancellationToken = default) {
            LastMoveRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, $"Moved messages to '{request.DestinationFolderId}'."));
        }

        public Task<MessageActionResult> DeleteAsync(DeleteMessagesRequest request, CancellationToken cancellationToken = default) {
            LastDeleteRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, "Deleted messages."));
        }

        private static MessageActionResult CreateResult(string profileId, IReadOnlyList<string> messageIds, string message) => new() {
            Succeeded = true,
            ProfileId = profileId,
            RequestedCount = messageIds.Count,
            SucceededCount = messageIds.Count,
            FailedCount = 0,
            Message = message,
            Results = messageIds.Select(id => new MessageActionItemResult {
                MessageId = id,
                Succeeded = true
            }).ToList()
        };
    }

    private sealed class FakeQueueService : IMailQueueService {
        public bool ProcessCalled { get; private set; }

        public Task<IReadOnlyList<QueuedMessageCompact>> ListCompactAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageCompact>>(new[] {
                new QueuedMessageCompact {
                    MessageId = "queued-1",
                    Provider = "gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    NextAttemptAt = DateTimeOffset.UtcNow,
                    AttemptCount = 0,
                    IsDue = true,
                    HasProviderData = false,
                    Summary = "queued-1 [gmail] attempts=0"
                }
            });

        public Task<IReadOnlyList<QueuedMessageSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageSummary>>(new[] {
                new QueuedMessageSummary {
                    MessageId = "queued-1",
                    Provider = "gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                    NextAttemptAt = DateTimeOffset.UtcNow,
                    AttemptCount = 0
                }
            });

        public Task<QueuedMessageCompact?> GetCompactAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageCompact?>(new QueuedMessageCompact {
                MessageId = messageId,
                Provider = "gmail",
                ProfileKind = MailProfileKind.Gmail,
                NextAttemptAt = DateTimeOffset.UtcNow,
                AttemptCount = 0,
                IsDue = true,
                HasProviderData = false,
                Summary = $"{messageId} [gmail] attempts=0"
            });

        public Task<QueuedMessageSummary?> GetAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageSummary?>(new QueuedMessageSummary {
                MessageId = messageId,
                Provider = "gmail",
                ProfileKind = MailProfileKind.Gmail,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                NextAttemptAt = DateTimeOffset.UtcNow,
                AttemptCount = 0
            });

        public Task<OperationResult> RemoveAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Queued message removed."));

        public Task<QueueProcessResult> ProcessAsync(CancellationToken cancellationToken = default) {
            ProcessCalled = true;
            return Task.FromResult(new QueueProcessResult {
                Succeeded = true,
                AttemptedCount = 1,
                SentCount = 1,
                FailedCount = 0,
                SkippedCount = 0,
                DroppedCount = 0,
                Message = "Queue processed."
            });
        }
    }

    private sealed class FakeProfileAuthService : IMailProfileAuthService {
        private readonly InMemoryProfileStore _profileStore;
        private readonly InMemorySecretStore _secretStore;

        public FakeProfileAuthService(InMemoryProfileStore profileStore, InMemorySecretStore secretStore) {
            _profileStore = profileStore;
            _secretStore = secretStore;
        }

        public GmailProfileLoginRequest? LastGmailRequest { get; private set; }

        public GraphProfileLoginRequest? LastGraphRequest { get; private set; }

        public string? LastRefreshProfileId { get; private set; }

        public string? LastStatusProfileId { get; private set; }

        public int RefreshCalls { get; private set; }

        public async Task<MailProfileAuthStatus?> GetStatusAsync(string profileId, CancellationToken cancellationToken = default) {
            LastStatusProfileId = profileId;
            var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken);
            if (profile == null) {
                return null;
            }

            var accessToken = await _secretStore.GetSecretAsync(profileId, MailSecretNames.AccessToken, cancellationToken);
            var refreshToken = await _secretStore.GetSecretAsync(profileId, MailSecretNames.RefreshToken, cancellationToken);
            var clientSecret = await _secretStore.GetSecretAsync(profileId, MailSecretNames.ClientSecret, cancellationToken);
            return new MailProfileAuthStatus {
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                AuthFlow = profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthFlow, out var authFlow) ? authFlow : MailProfileAuthFlowNames.Interactive,
                Mode = profile.Kind == MailProfileKind.Graph ? "appOnly" : "interactive",
                Mailbox = profile.DefaultMailbox,
                HasAccessToken = !string.IsNullOrWhiteSpace(accessToken),
                HasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken),
                HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
                CanRefresh = profile.Kind is MailProfileKind.Gmail or MailProfileKind.Graph,
                CanLoginInteractively = profile.Kind is MailProfileKind.Gmail or MailProfileKind.Graph,
                Summary = $"{profile.Id} [{profile.Kind}] auth status available."
            };
        }

        public async Task<MailProfileAuthenticationResult> LoginGmailAsync(GmailProfileLoginRequest request, CancellationToken cancellationToken = default) {
            var profile = await _profileStore.GetByIdAsync(request.ProfileId, cancellationToken);
            Assert.NotNull(profile);
            var savedProfile = profile!;
            var account = request.GmailAccount
                ?? (savedProfile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) ? mailbox : null)
                ?? "user@gmail.com";
            LastGmailRequest = new GmailProfileLoginRequest {
                ProfileId = request.ProfileId,
                GmailAccount = account,
                ClientId = request.ClientId ?? (savedProfile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId) ? clientId : null),
                ClientSecret = request.ClientSecret,
                Scopes = request.Scopes
            };
            savedProfile.Settings[MailProfileSettingsKeys.Mailbox] = account;
            savedProfile.DefaultMailbox = account;
            await _profileStore.SaveAsync(savedProfile, cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.AccessToken, "gmail-access-token", cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.RefreshToken, "gmail-refresh-token", cancellationToken);
            return new MailProfileAuthenticationResult {
                Succeeded = true,
                Message = "Gmail login completed.",
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Gmail,
                UserName = account
            };
        }

        public async Task<MailProfileAuthenticationResult> LoginGraphAsync(GraphProfileLoginRequest request, CancellationToken cancellationToken = default) {
            LastGraphRequest = request;
            var profile = await _profileStore.GetByIdAsync(request.ProfileId, cancellationToken);
            var mailbox = request.Mailbox ?? request.Login ?? "user@example.com";
            profile!.Settings[MailProfileSettingsKeys.Mailbox] = mailbox;
            profile.DefaultMailbox = mailbox;
            await _profileStore.SaveAsync(profile, cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.AccessToken, "graph-access-token", cancellationToken);
            return new MailProfileAuthenticationResult {
                Succeeded = true,
                Message = "Graph login completed.",
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Graph,
                UserName = mailbox
            };
        }

        public async Task<MailProfileAuthenticationResult> RefreshAsync(string profileId, CancellationToken cancellationToken = default) {
            RefreshCalls++;
            LastRefreshProfileId = profileId;
            var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken);
            Assert.NotNull(profile);
            return profile!.Kind switch {
                MailProfileKind.Gmail => await LoginGmailAsync(new GmailProfileLoginRequest { ProfileId = profileId }, cancellationToken),
                MailProfileKind.Graph => await LoginGraphAsync(new GraphProfileLoginRequest { ProfileId = profileId }, cancellationToken),
                _ => new MailProfileAuthenticationResult {
                    Succeeded = false,
                    Code = "refresh_not_supported",
                    Message = "Refresh not supported.",
                    ProfileId = profileId,
                    ProfileKind = profile.Kind
                }
            };
        }
    }

    private sealed class FakeProfileConnectionService : IMailProfileConnectionService {
        public string? LastProfileId { get; private set; }

        public MailProfileConnectionTestScope LastScope { get; private set; }

        public Task<MailProfileConnectionTestResult> TestAsync(
            string profileId,
            MailProfileConnectionTestScope scope = MailProfileConnectionTestScope.Auto,
            CancellationToken cancellationToken = default) {
            LastProfileId = profileId;
            LastScope = scope;
            return Task.FromResult(new MailProfileConnectionTestResult {
                Succeeded = true,
                Message = "Profile connection succeeded.",
                ProfileId = profileId,
                ProfileKind = MailProfileKind.Gmail,
                Probe = "getProfile",
                Target = "gmail-work",
                RequestedScope = scope,
                ExecutedScope = scope == MailProfileConnectionTestScope.Auto ? MailProfileConnectionTestScope.Mailbox : scope
            });
        }
    }
}
#endif
