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
        public GetMessageRequest? LastGetCompactRequest { get; private set; }

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

        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    Id = "Inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox"
                }
            });

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
