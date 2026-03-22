#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr.Application;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed class CliRunnerTests {
    [Fact]
    public async Task HelpIsShownWhenNoArgumentsAreProvided() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(Array.Empty<string>(), stdout, stderr, _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("Mailozaurr CLI", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileListUsesApplicationProfilesService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("work-imap", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileListSummaryUsesSharedOverviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--summary", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("work-imap", fixture.ProfileAuthService.LastStatusProfileId);
        Assert.Contains("\"SupportsRead\": true", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Summary\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileListSummaryCompactUsesSharedCompactProjection() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--summary", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Id\": \"work-imap\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"Profile\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileListSummarySupportsSharedFilters() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--summary", "--kind", "imap", "--can-read", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Profile\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Kind\": 1", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"SupportsSend\": true", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileListSummarySupportsSharedSorting() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "z-smtp",
            DisplayName = "SMTP Z",
            Kind = MailProfileKind.Smtp,
            DefaultSender = "alerts@example.com",
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Server] = "smtp.example.com"
            }
        });

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "list", "--summary", "--sort", "id", "--desc", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Id\": \"z-smtp\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.True(stdout.ToString().IndexOf("\"Id\": \"z-smtp\"", StringComparison.Ordinal) <
                    stdout.ToString().IndexOf("\"Id\": \"work-imap\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProfileCreateSavesProfileThroughApplicationService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "create",
                "--profile", "alerts-imap",
                "--kind", "imap",
                "--name", "Alerts IMAP",
                "--default-mailbox", "alerts@example.com",
                "--setting", "server=imap.alerts.example.com",
                "--setting", "port=993",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var created = await fixture.ProfileStore.GetByIdAsync("alerts-imap");

        Assert.Equal(0, exitCode);
        Assert.NotNull(created);
        Assert.Equal(MailProfileKind.Imap, created!.Kind);
        Assert.Equal("Alerts IMAP", created.DisplayName);
        Assert.Equal("alerts@example.com", created.DefaultMailbox);
        Assert.Equal("imap.alerts.example.com", created.Settings[MailProfileSettingsKeys.Server]);
        Assert.Equal("993", created.Settings[MailProfileSettingsKeys.Port]);
    }

    [Fact]
    public async Task ProfileGraphBootstrapSavesProfileAndSecretsThroughApplicationServices() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "graph-bootstrap",
                "--profile", "graph-work",
                "--name", "Work Graph",
                "--mailbox", "shared@example.com",
                "--client-id", "client-id",
                "--tenant-id", "tenant-id",
                "--client-secret", "client-secret",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var profile = await fixture.ProfileStore.GetByIdAsync("graph-work");
        var clientSecret = await fixture.SecretStore.GetSecretAsync("graph-work", MailSecretNames.ClientSecret);

        Assert.Equal(0, exitCode);
        Assert.NotNull(profile);
        Assert.Equal(MailProfileKind.Graph, profile!.Kind);
        Assert.Equal("shared@example.com", profile.DefaultMailbox);
        Assert.Equal("shared@example.com", profile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal("client-id", profile.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("tenant-id", profile.Settings[MailProfileSettingsKeys.TenantId]);
        Assert.Equal("client-secret", clientSecret);
    }

    [Fact]
    public async Task ProfileGmailBootstrapSavesProfileAndSecretsThroughApplicationServices() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "gmail-bootstrap",
                "--profile", "gmail-work",
                "--name", "Work Gmail",
                "--mailbox", "me@example.com",
                "--client-id", "client-id",
                "--client-secret", "client-secret",
                "--refresh-token", "refresh-token",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var profile = await fixture.ProfileStore.GetByIdAsync("gmail-work");
        var clientSecret = await fixture.SecretStore.GetSecretAsync("gmail-work", MailSecretNames.ClientSecret);
        var refreshToken = await fixture.SecretStore.GetSecretAsync("gmail-work", MailSecretNames.RefreshToken);

        Assert.Equal(0, exitCode);
        Assert.NotNull(profile);
        Assert.Equal(MailProfileKind.Gmail, profile!.Kind);
        Assert.Equal("me@example.com", profile.DefaultMailbox);
        Assert.Equal("me@example.com", profile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal("client-id", profile.Settings[MailProfileSettingsKeys.ClientId]);
        Assert.Equal("client-secret", clientSecret);
        Assert.Equal("refresh-token", refreshToken);
    }

    [Fact]
    public async Task ProfileDoctorReportsMissingGmailAuthenticationMaterial() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
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

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "doctor", "--profile", "gmail-broken", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(1, exitCode);
        Assert.Contains("profile_not_ready", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Gmail profiles need an access token", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileGmailLoginPersistsTokensThroughApplicationService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
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

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "gmail-login", "--profile", "gmail-login", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var accessToken = await fixture.SecretStore.GetSecretAsync("gmail-login", MailSecretNames.AccessToken);
        var refreshToken = await fixture.SecretStore.GetSecretAsync("gmail-login", MailSecretNames.RefreshToken);

        Assert.Equal(0, exitCode);
        Assert.Equal("gmail-access-token", accessToken);
        Assert.Equal("gmail-refresh-token", refreshToken);
        Assert.NotNull(fixture.ProfileAuthService.LastGmailRequest);
        Assert.Equal("user@gmail.com", fixture.ProfileAuthService.LastGmailRequest!.GmailAccount);
    }

    [Fact]
    public async Task ProfileGraphLoginPersistsAccessTokenThroughApplicationService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "graph-login",
            DisplayName = "Graph Login",
            Kind = MailProfileKind.Graph,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.ClientId] = "client-id",
                [MailProfileSettingsKeys.TenantId] = "tenant-id"
            }
        });

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "graph-login", "--profile", "graph-login", "--login", "user@example.com", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var accessToken = await fixture.SecretStore.GetSecretAsync("graph-login", MailSecretNames.AccessToken);
        var profile = await fixture.ProfileStore.GetByIdAsync("graph-login");

        Assert.Equal(0, exitCode);
        Assert.Equal("graph-access-token", accessToken);
        Assert.NotNull(profile);
        Assert.Equal("user@example.com", profile!.DefaultMailbox);
        Assert.Equal("user@example.com", profile.Settings[MailProfileSettingsKeys.Mailbox]);
        Assert.Equal("https://login.microsoftonline.com/common/oauth2/nativeclient", profile.Settings[MailProfileSettingsKeys.RedirectUri]);
        Assert.NotNull(fixture.ProfileAuthService.LastGraphRequest);
        Assert.Equal("user@example.com", fixture.ProfileAuthService.LastGraphRequest!.Login);
    }

    [Fact]
    public async Task ProfileRefreshAuthDelegatesToSharedAuthService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
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

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "refresh-auth", "--profile", "gmail-refresh", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fixture.ProfileAuthService.RefreshCalls);
        Assert.Equal("gmail-refresh", fixture.ProfileAuthService.LastRefreshProfileId);
        Assert.Contains("ProfileId", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileAuthStatusDelegatesToSharedAuthService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "auth-status", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("work-imap", fixture.ProfileAuthService.LastStatusProfileId);
        Assert.Contains("\"Mode\": \"basic\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileSummaryUsesSharedOverviewService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "summary", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("work-imap", fixture.ProfileAuthService.LastStatusProfileId);
        Assert.Contains("\"SupportsRead\": true", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"IsReady\": true", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileSummaryCompactUsesSharedCompactProjection() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "summary", "--profile", "work-imap", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Id\": \"work-imap\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"Profile\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileTestDelegatesToSharedConnectionService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "test", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("work-imap", fixture.ProfileConnectionService.LastProfileId);
        Assert.Contains("\"Probe\": \"connect\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileTestParsesRequestedScope() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "test", "--profile", "work-imap", "--scope", "send", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(MailProfileConnectionTestScope.Send, fixture.ProfileConnectionService.LastScope);
        Assert.Contains("\"RequestedScope\": 3", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileCapabilitiesUsesApplicationProfileService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "capabilities", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Kind\": 1", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"Capabilities\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileSetSecretPersistsSecretThroughApplicationService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "set-secret",
                "--profile", "work-imap",
                "--name", MailSecretNames.Password,
                "--value", "super-secret",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var secretValue = await fixture.SecretStore.GetSecretAsync("work-imap", MailSecretNames.Password);

        Assert.Equal(0, exitCode);
        Assert.Equal("super-secret", secretValue);
    }

    [Fact]
    public async Task ProfileRemoveSecretDeletesSecretThroughApplicationService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        await fixture.SecretStore.SetSecretAsync("work-imap", MailSecretNames.Password, "super-secret");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "remove-secret",
                "--profile", "work-imap",
                "--name", MailSecretNames.Password,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var secretValue = await fixture.SecretStore.GetSecretAsync("work-imap", MailSecretNames.Password);

        Assert.Equal(0, exitCode);
        Assert.Null(secretValue);
    }

    [Fact]
    public async Task ProfileDeleteRemovesProfileThroughApplicationService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "delete", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var profile = await fixture.ProfileStore.GetByIdAsync("work-imap");

        Assert.Equal(0, exitCode);
        Assert.Null(profile);
    }

    [Fact]
    public async Task ProfileSetDefaultUpdatesProfileThroughApplicationService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "profile", "set-default", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var profile = await fixture.ProfileStore.GetByIdAsync("work-imap");

        Assert.Equal(0, exitCode);
        Assert.NotNull(profile);
        Assert.True(profile!.IsDefault);
    }

    [Fact]
    public async Task MailFoldersUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "folders", "--profile", "work-imap", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Inbox\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailFoldersCompactUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "folders", "--profile", "work-imap", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastFolderCompactQuery);
        Assert.Equal("work-imap", fixture.ReadService.LastFolderCompactQuery!.ProfileId);
        Assert.Contains("\"Summary\": \"inbox Inbox\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailSearchUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "search", "--profile", "work-imap", "--query", "reports", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"msg-1\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailSearchCompactUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "mail", "search", "--profile", "work-imap", "--query", "reports", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastSearchCompactRequest);
        Assert.Equal("work-imap", fixture.ReadService.LastSearchCompactRequest!.ProfileId);
        Assert.Equal("reports", fixture.ReadService.LastSearchCompactRequest.QueryText);
        Assert.Contains("\"Summary\": \"msg-1 reports\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailGetPassesMailboxToApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "get",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastGetRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastGetRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastGetRequest.MessageId);
    }

    [Fact]
    public async Task MailGetCompactUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "get",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--compact",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastGetCompactRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastGetCompactRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastGetCompactRequest.MessageId);
        Assert.Contains("\"SummaryText\": \"msg-42 Subject\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailAttachmentsUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "attachments",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastListAttachmentsRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastListAttachmentsRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastListAttachmentsRequest.MessageId);
        Assert.Contains("\"report.pdf\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MailSaveAttachmentPassesMailboxToApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "save-attachment",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--attachment-id", "1",
                "--path", "C:\\Temp\\report.pdf",
                "--overwrite",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastSaveAttachmentRequest!.MailboxId);
        Assert.Equal("1", fixture.ReadService.LastSaveAttachmentRequest.AttachmentId);
        Assert.True(fixture.ReadService.LastSaveAttachmentRequest.Overwrite);
    }

    [Fact]
    public async Task MailSaveAttachmentsUsesApplicationReadService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "mail", "save-attachments",
                "--profile", "work-imap",
                "--mailbox", "shared@example.com",
                "--message-id", "msg-42",
                "--path", "C:\\Temp",
                "--attachment-id", "att-1",
                "--name-contains", "report",
                "--content-type", "pdf",
                "--overwrite",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ReadService.LastSaveAttachmentsRequest);
        Assert.Equal("shared@example.com", fixture.ReadService.LastSaveAttachmentsRequest!.MailboxId);
        Assert.Equal("msg-42", fixture.ReadService.LastSaveAttachmentsRequest.MessageId);
        Assert.Equal("C:\\Temp", fixture.ReadService.LastSaveAttachmentsRequest.DestinationPath);
        Assert.Contains("att-1", fixture.ReadService.LastSaveAttachmentsRequest.AttachmentIds);
        Assert.Equal("report", fixture.ReadService.LastSaveAttachmentsRequest.FileNameContains);
        Assert.Equal("pdf", fixture.ReadService.LastSaveAttachmentsRequest.ContentTypeContains);
        Assert.True(fixture.ReadService.LastSaveAttachmentsRequest.Overwrite);
    }

    [Fact]
    public async Task QueueListUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "list", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"queued-1\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueListCompactUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "list", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"MessageId\": \"queued-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"QueuedAt\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueGetUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "get", "--message-id", "queued-1", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("queued-1", fixture.QueueService.LastGetMessageId);
    }

    [Fact]
    public async Task QueueGetCompactUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "get", "--message-id", "queued-1", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"MessageId\": \"queued-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"QueuedAt\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueProcessUsesApplicationQueueService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "queue", "process", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(1, fixture.QueueService.ProcessCalls);
    }

    [Fact]
    public async Task DraftSaveUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "draft", "save",
                "--draft", "draft-1",
                "--name", "Quarterly report",
                "--profile", "work-imap",
                "--to", "alice@example.com",
                "--subject", "Quarterly report",
                "--text", "Body",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.DraftService.LastSavedDraft);
        Assert.Equal("draft-1", fixture.DraftService.LastSavedDraft!.Id);
        Assert.Equal("work-imap", fixture.DraftService.LastSavedDraft.Message.ProfileId);
    }

    [Fact]
    public async Task DraftListCompactUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "draft", "list", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Id\": \"draft-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"Message\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftGetCompactUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] { "draft", "get", "--draft", "draft-1", "--compact", "--json" },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Contains("\"Id\": \"draft-1\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"Message\":", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftSaveFromFileUsesDraftExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var importPath = CreateTemporaryFilePath("import-draft.json");
        await File.WriteAllTextAsync(importPath, JsonSerializer.Serialize(new MailDraft {
            Id = "imported-draft",
            Name = "Imported draft",
            Message = new DraftMessage {
                ProfileId = "work-imap",
                Subject = "Imported subject",
                To = {
                    new MessageRecipient { Address = "imported@example.com" }
                }
            }
        }));

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "draft", "save",
                "--file", importPath,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(importPath, fixture.DraftExchangeService.LastLoadedPath);
        Assert.NotNull(fixture.DraftService.LastSavedDraft);
        Assert.Equal("imported-draft", fixture.DraftService.LastSavedDraft!.Id);
    }

    [Fact]
    public async Task DraftExportUsesDraftExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var exportPath = CreateTemporaryFilePath("export-draft.json");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "draft", "export",
                "--draft", "draft-1",
                "--path", exportPath,
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("draft-1", fixture.DraftService.LastRequestedDraftId);
        Assert.Equal(exportPath, fixture.DraftExchangeService.LastSavedPath);
    }

    [Fact]
    public async Task SendUsingDraftUsesApplicationDraftService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "send",
                "--draft", "draft-1",
                "--send-now",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal("draft-1", fixture.DraftService.LastRequestedDraftId);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("work-imap", fixture.SendService.LastRequest!.ProfileId);
        Assert.Equal("saved@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
    }

    [Fact]
    public async Task SendUsingFileUsesDraftExchangeService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        var importPath = CreateTemporaryFilePath("send-draft.json");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "send",
                "--file", importPath,
                "--send-now",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.Equal(importPath, fixture.DraftExchangeService.LastLoadedPath);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("work-imap", fixture.SendService.LastRequest!.ProfileId);
        Assert.True(fixture.SendService.LastRequest.RequireImmediateSend);
        Assert.False(fixture.SendService.LastRequest.PreferQueue);
        Assert.Equal("imported@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("Imported subject", fixture.SendService.LastRequest.Message.Subject);
    }

    [Fact]
    public async Task SendUsesApplicationSendService() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "send",
                "--profile", "work-imap",
                "--from", "sender@example.com",
                "--to", "alice@example.com",
                "--to", "bob@example.com",
                "--cc", "carol@example.com",
                "--reply-to", "reply@example.com",
                "--subject", "Quarterly report",
                "--text", "Plain text body",
                "--html", "<b>HTML body</b>",
                "--header", "X-Test=value",
                "--attachment", "C:\\Temp\\report.pdf",
                "--send-now",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.SendService.LastRequest);
        Assert.Equal("work-imap", fixture.SendService.LastRequest!.ProfileId);
        Assert.True(fixture.SendService.LastRequest.RequireImmediateSend);
        Assert.False(fixture.SendService.LastRequest.PreferQueue);
        Assert.Equal("sender@example.com", fixture.SendService.LastRequest.Message.From!.Address);
        Assert.Equal(2, fixture.SendService.LastRequest.Message.To.Count);
        Assert.Equal("alice@example.com", fixture.SendService.LastRequest.Message.To[0].Address);
        Assert.Equal("carol@example.com", fixture.SendService.LastRequest.Message.Cc[0].Address);
        Assert.Equal("reply@example.com", fixture.SendService.LastRequest.Message.ReplyTo[0].Address);
        Assert.Equal("Quarterly report", fixture.SendService.LastRequest.Message.Subject);
        Assert.Equal("Plain text body", fixture.SendService.LastRequest.Message.TextBody);
        Assert.Equal("<b>HTML body</b>", fixture.SendService.LastRequest.Message.HtmlBody);
        Assert.Equal("value", fixture.SendService.LastRequest.Message.Headers["X-Test"]);
        Assert.Equal("C:\\Temp\\report.pdf", fixture.SendService.LastRequest.Message.Attachments[0].Path);
    }

    private static TestApplicationFixture CreateFixture() => new();

    private sealed class InMemoryProfileStore : IMailProfileStore {
        private readonly Dictionary<string, MailProfile> _profiles;

        public InMemoryProfileStore(IEnumerable<MailProfile> profiles) {
            _profiles = profiles.ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(_profiles.Values.ToArray());

        public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
            _profiles.TryGetValue(profileId, out var profile);
            return Task.FromResult(profile);
        }

        public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Remove(profileId));

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            _profiles[profile.Id] = profile;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySecretStore : IMailSecretStore {
        private readonly Dictionary<string, Dictionary<string, string>> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            if (_secrets.TryGetValue(profileId, out var profileSecrets) &&
                profileSecrets.TryGetValue(secretName, out var secretValue)) {
                return Task.FromResult<string?>(secretValue);
            }

            return Task.FromResult<string?>(null);
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            var removed = _secrets.TryGetValue(profileId, out var profileSecrets) &&
                          profileSecrets.Remove(secretName);
            return Task.FromResult(removed);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            if (!_secrets.TryGetValue(profileId, out var profileSecrets)) {
                profileSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _secrets[profileId] = profileSecrets;
            }

            profileSecrets[secretName] = secretValue;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReadService : IMailReadService {
        public GetMessageRequest? LastGetCompactRequest { get; private set; }

        public SaveAttachmentsRequest? LastSaveAttachmentsRequest { get; private set; }

        public ListAttachmentsRequest? LastListAttachmentsRequest { get; private set; }

        public MailFolderQuery? LastFolderCompactQuery { get; private set; }

        public MailSearchRequest? LastSearchCompactRequest { get; private set; }

        public GetMessageRequest? LastGetRequest { get; private set; }

        public SaveAttachmentRequest? LastSaveAttachmentRequest { get; private set; }

        public Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(CaptureGetRequest(request));

        public Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default) {
            LastGetCompactRequest = request;
            return Task.FromResult<MessageDetailCompact?>(new MessageDetailCompact {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Subject",
                    Summary = $"{request.MessageId} Subject"
                },
                TextBodyPreview = "Body",
                SummaryText = $"{request.MessageId} Subject"
            });
        }

        private MessageDetail CaptureGetRequest(GetMessageRequest request) {
            LastGetRequest = request;
            return new MessageDetail {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Subject"
                },
                TextBody = "Body"
            };
        }

        public Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
            LastFolderCompactQuery = query;
            return Task.FromResult<IReadOnlyList<FolderRefCompact>>(new[] {
                new FolderRefCompact {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox",
                    Summary = "inbox Inbox"
                }
            });
        }

        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox"
                }
            });

        public Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default) {
            LastSaveAttachmentRequest = request;
            return Task.FromResult(OperationResult.Success("Attachment saved."));
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
                        AttachmentId = "att-1",
                        FileName = "report.pdf",
                        ContentType = "application/pdf"
                    }
                }
            });
        }

        public Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
            LastSearchCompactRequest = request;
            return Task.FromResult<IReadOnlyList<MessageSummaryCompact>>(new[] {
                new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = "msg-1",
                    Subject = request.QueryText,
                    Summary = $"msg-1 {request.QueryText}"
                }
            });
        }

        public Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default) {
            LastListAttachmentsRequest = request;
            return Task.FromResult<IReadOnlyList<AttachmentSummary>>(new[] {
                new AttachmentSummary {
                    MessageId = request.MessageId,
                    Id = "att-1",
                    FileName = "report.pdf",
                    SizeInBytes = 2048
                }
            });
        }

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = "msg-1",
                    Subject = request.QueryText
                }
            });
    }

    private sealed class FakeSendService : IMailSendService {
        public SendMessageRequest? LastRequest { get; private set; }

        public Task<SendResult> SendAsync(SendMessageRequest request, CancellationToken cancellationToken = default) {
            LastRequest = request;
            return Task.FromResult(new SendResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Imap,
                ProviderMessageId = "provider-1",
                Message = "Message sent successfully."
            });
        }
    }

    private sealed class FakeQueueService : IMailQueueService {
        public string? LastGetMessageId { get; private set; }

        public string? LastRemoveMessageId { get; private set; }

        public int ProcessCalls { get; private set; }

        public Task<QueuedMessageCompact?> GetCompactAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageCompact?>(new QueuedMessageCompact {
                MessageId = messageId,
                Provider = "Gmail",
                ProfileKind = MailProfileKind.Gmail,
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
                AttemptCount = 0,
                IsDue = false,
                HasProviderData = false,
                Summary = $"{messageId} [Gmail] attempts=0"
            });

        public Task<QueuedMessageSummary?> GetAsync(string messageId, CancellationToken cancellationToken = default) {
            LastGetMessageId = messageId;
            return Task.FromResult<QueuedMessageSummary?>(new QueuedMessageSummary {
                MessageId = messageId,
                Provider = "Gmail",
                ProfileKind = MailProfileKind.Gmail,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        }

        public Task<IReadOnlyList<QueuedMessageCompact>> ListCompactAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageCompact>>(new[] {
                new QueuedMessageCompact {
                    MessageId = "queued-1",
                    Provider = "Gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    AttemptCount = 1,
                    IsDue = false,
                    HasProviderData = true,
                    Summary = "queued-1 [Gmail] attempts=1"
                }
            });

        public Task<IReadOnlyList<QueuedMessageSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageSummary>>(new[] {
                new QueuedMessageSummary {
                    MessageId = "queued-1",
                    Provider = "Gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    AttemptCount = 1,
                    HasProviderData = true
                }
            });

        public Task<QueueProcessResult> ProcessAsync(CancellationToken cancellationToken = default) {
            ProcessCalls++;
            return Task.FromResult(new QueueProcessResult {
                Succeeded = true,
                AttemptedCount = 1,
                SentCount = 1,
                Message = "Queue processing completed."
            });
        }

        public Task<OperationResult> RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            LastRemoveMessageId = messageId;
            return Task.FromResult(OperationResult.Success("Queued message removed."));
        }
    }

    private sealed class FakeDraftService : IMailDraftService {
        public string? LastRequestedDraftId { get; private set; }

        public MailDraft? LastSavedDraft { get; private set; }

        public Task<MailDraftCompact?> GetDraftCompactAsync(string draftId, CancellationToken cancellationToken = default) {
            LastRequestedDraftId = draftId;
            return Task.FromResult<MailDraftCompact?>(new MailDraftCompact {
                Id = draftId,
                Name = "Saved draft",
                ProfileId = "work-imap",
                Subject = "Saved subject",
                ToCount = 1,
                AttachmentCount = 0,
                UpdatedAt = DateTimeOffset.UtcNow,
                Summary = $"{draftId} [work-imap] Saved draft"
            });
        }

        public Task<MailDraft?> GetDraftAsync(string draftId, CancellationToken cancellationToken = default) {
            LastRequestedDraftId = draftId;
            return Task.FromResult<MailDraft?>(new MailDraft {
                Id = draftId,
                Name = "Saved draft",
                Message = new DraftMessage {
                    ProfileId = "work-imap",
                    Subject = "Saved subject",
                    To = {
                        new MessageRecipient { Address = "saved@example.com" }
                    }
                }
            });
        }

        public Task<IReadOnlyList<MailDraftCompact>> GetDraftsCompactAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailDraftCompact>>(new[] {
                new MailDraftCompact {
                    Id = "draft-1",
                    Name = "Saved draft",
                    ProfileId = "work-imap",
                    Subject = "Saved subject",
                    ToCount = 0,
                    AttachmentCount = 0,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Summary = "draft-1 [work-imap] Saved draft"
                }
            });

        public Task<IReadOnlyList<MailDraft>> GetDraftsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailDraft>>(new[] {
                new MailDraft {
                    Id = "draft-1",
                    Name = "Saved draft",
                    Message = new DraftMessage {
                        ProfileId = "work-imap",
                        Subject = "Saved subject"
                    }
                }
            });

        public Task<OperationResult> SaveAsync(MailDraft draft, CancellationToken cancellationToken = default) {
            LastSavedDraft = draft;
            return Task.FromResult(OperationResult.Success("Draft saved."));
        }

        public Task<OperationResult> DeleteAsync(string draftId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Draft deleted."));
    }

    private sealed class FakeDraftExchangeService : IMailDraftExchangeService {
        public string? LastLoadedPath { get; private set; }

        public string? LastSavedPath { get; private set; }

        public Task<MailDraft> LoadAsync(string path, CancellationToken cancellationToken = default) {
            LastLoadedPath = path;
            return Task.FromResult(new MailDraft {
                Id = "imported-draft",
                Name = "Imported draft",
                Message = new DraftMessage {
                    ProfileId = "work-imap",
                    Subject = "Imported subject",
                    To = {
                        new MessageRecipient { Address = "imported@example.com" }
                    }
                }
            });
        }

        public Task SaveAsync(string path, MailDraft draft, CancellationToken cancellationToken = default) {
            LastSavedPath = path;
            return Task.CompletedTask;
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
            var password = await _secretStore.GetSecretAsync(profileId, MailSecretNames.Password, cancellationToken);

            return new MailProfileAuthStatus {
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                AuthFlow = profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthFlow, out var authFlow) ? authFlow : null,
                Mode = profile.Kind == MailProfileKind.Imap && !string.IsNullOrWhiteSpace(password) ? "basic" : "interactive",
                Mailbox = profile.DefaultMailbox,
                HasAccessToken = !string.IsNullOrWhiteSpace(accessToken),
                HasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken),
                HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
                HasPassword = !string.IsNullOrWhiteSpace(password),
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
            savedProfile.Settings[MailProfileSettingsKeys.ClientId] = request.ClientId ?? savedProfile.Settings[MailProfileSettingsKeys.ClientId];
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
            profile.Settings[MailProfileSettingsKeys.RedirectUri] = request.RedirectUri ?? "https://login.microsoftonline.com/common/oauth2/nativeclient";
            profile.DefaultMailbox = mailbox;
            await _profileStore.SaveAsync(profile, cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.AccessToken, "graph-access-token", cancellationToken);
            return new MailProfileAuthenticationResult {
                Succeeded = true,
                Message = "Graph login completed.",
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Graph,
                UserName = request.Login ?? mailbox
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
                ProfileKind = MailProfileKind.Imap,
                Probe = "connect",
                Target = "work@example.com",
                RequestedScope = scope,
                ExecutedScope = scope == MailProfileConnectionTestScope.Auto ? MailProfileConnectionTestScope.Auth : scope
            });
        }
    }

    private sealed class TestApplicationFixture {
        public TestApplicationFixture() {
            ProfileStore = new InMemoryProfileStore(new[] {
                new MailProfile {
                    Id = "work-imap",
                    DisplayName = "Work IMAP",
                    Kind = MailProfileKind.Imap,
                    Settings = new Dictionary<string, string> {
                        [MailProfileSettingsKeys.Server] = "imap.example.com"
                    }
                }
            });
            SecretStore = new InMemorySecretStore();
            SecretStore.SetSecretAsync("work-imap", MailSecretNames.Password, "secret").GetAwaiter().GetResult();
            ProfileAuthService = new FakeProfileAuthService(ProfileStore, SecretStore);
        }

        public InMemoryProfileStore ProfileStore { get; }

        public InMemorySecretStore SecretStore { get; }

        public FakeReadService ReadService { get; } = new();

        public FakeQueueService QueueService { get; } = new();

        public FakeSendService SendService { get; } = new();

        public FakeDraftService DraftService { get; } = new();

        public FakeDraftExchangeService DraftExchangeService { get; } = new();

        public FakeProfileAuthService ProfileAuthService { get; }

        public FakeProfileConnectionService ProfileConnectionService { get; } = new();

        public MailApplicationBuilder CreateBuilder() =>
            new MailApplicationBuilder()
                .UseProfileStore(ProfileStore)
                .UseSecretStore(SecretStore)
                .UseDraftStore(new FileMailDraftStore(CreateTemporaryFilePath("drafts.json")))
                .UseProfileService(new MailProfileService(ProfileStore, SecretStore))
                .UseProfileConnectionService(ProfileConnectionService)
                .UseProfileSecretService(new MailProfileSecretService(ProfileStore, SecretStore))
                .UseProfileAuthService(ProfileAuthService)
                .UseDraftService(DraftService)
                .UseDraftExchangeService(DraftExchangeService)
                .UseReadService(ReadService)
                .UseQueueService(QueueService)
                .UseSendService(SendService);

        private static string CreateTemporaryFilePath(string fileName) {
            var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
#endif
