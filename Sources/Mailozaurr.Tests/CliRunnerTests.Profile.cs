#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr.Application;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
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
                "--client-secret-stdin",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder(),
            new StringReader("client-secret"));

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
    public async Task ProfileGraphBootstrapRejectsMultipleStdinSecretSources() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "graph-bootstrap",
                "--profile", "graph-invalid-stdin",
                "--name", "Invalid Graph",
                "--mailbox", "shared@example.com",
                "--client-secret-stdin",
                "--access-token-stdin",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder(),
            new StringReader("one-value"));

        Assert.Equal(1, exitCode);
        Assert.Contains("Standard input can supply only one secret", stderr.ToString(), StringComparison.Ordinal);
        Assert.Null(await fixture.ProfileStore.GetByIdAsync("graph-invalid-stdin"));
    }

    [Fact]
    public async Task ProfileGraphBootstrapSupportsSecretValuesFromEnvironmentVariables() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        const string envName = "MAILOZAURR_TEST_GRAPH_SECRET";
        Environment.SetEnvironmentVariable(envName, "env-client-secret");

        try {
            var exitCode = await CliRunner.RunAsync(
                new[] {
                    "profile", "graph-bootstrap",
                    "--profile", "graph-env",
                    "--name", "Graph Env",
                    "--mailbox", "shared@example.com",
                    "--client-id", "client-id",
                    "--tenant-id", "tenant-id",
                    "--client-secret-env", envName,
                    "--json"
                },
                stdout,
                stderr,
                _ => fixture.CreateBuilder());

            var clientSecret = await fixture.SecretStore.GetSecretAsync("graph-env", MailSecretNames.ClientSecret);

            Assert.Equal(0, exitCode);
            Assert.Equal("env-client-secret", clientSecret);
        } finally {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public async Task ProfileGraphBootstrapSupportsSecretReferences() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
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

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "graph-bootstrap",
                "--profile", "graph-ref",
                "--name", "Graph Ref",
                "--mailbox", "shared@example.com",
                "--client-id", "client-id",
                "--tenant-id", "tenant-id",
                "--client-secret-ref", $"shared-secrets:{MailSecretNames.ClientSecret}",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var clientSecret = await fixture.SecretStore.GetSecretAsync("graph-ref", MailSecretNames.ClientSecret);

        Assert.Equal(0, exitCode);
        Assert.Equal("shared-client-secret", clientSecret);
    }

    [Fact]
    public async Task ProfileGmailBootstrapSavesProfileAndSecretsThroughApplicationServices() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        await fixture.SecretStore.SetSecretAsync("shared-secrets", MailSecretNames.ClientSecret, "client-secret");
        await fixture.SecretStore.SetSecretAsync("shared-secrets", MailSecretNames.RefreshToken, "refresh-token");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "gmail-bootstrap",
                "--profile", "gmail-work",
                "--name", "Work Gmail",
                "--mailbox", "me@example.com",
                "--client-id", "client-id",
                "--client-secret-ref", $"shared-secrets:{MailSecretNames.ClientSecret}",
                "--refresh-token-ref", $"shared-secrets:{MailSecretNames.RefreshToken}",
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
    public async Task ProfileSetSecretSupportsReadingValueFromStandardInput() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var stdin = new StringReader("stdin-secret\r\n");
        var fixture = CreateFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "stdin-profile",
            DisplayName = "stdin",
            Kind = MailProfileKind.Imap
        });

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "set-secret",
                "--profile", "stdin-profile",
                "--name", "password",
                "--value-stdin",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder(),
            stdin);

        var secret = await fixture.SecretStore.GetSecretAsync("stdin-profile", "password");

        Assert.Equal(0, exitCode);
        Assert.Equal("stdin-secret", secret);
    }

    [Fact]
    public async Task JsonErrorsAreWrittenAsStructuredPayloads() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "set-secret",
                "--profile", "missing-value",
                "--name", "password",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        using var document = JsonDocument.Parse(stderr.ToString());

        Assert.Equal(1, exitCode);
        Assert.Equal("InvalidOperationException", document.RootElement.GetProperty("Error").GetProperty("Type").GetString());
        Assert.Contains("--value-env", document.RootElement.GetProperty("Error").GetProperty("Message").GetString(), StringComparison.Ordinal);
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
    public async Task ProfileGmailLoginSupportsClientSecretReference() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "gmail-login-ref",
            DisplayName = "Gmail Login Ref",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "user@gmail.com",
                [MailProfileSettingsKeys.ClientId] = "client-id"
            }
        });
        await fixture.SecretStore.SetSecretAsync("shared-secrets", MailSecretNames.ClientSecret, "client-secret-from-reference");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "gmail-login",
                "--profile", "gmail-login-ref",
                "--client-secret-ref", $"shared-secrets:{MailSecretNames.ClientSecret}",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        Assert.Equal(0, exitCode);
        Assert.NotNull(fixture.ProfileAuthService.LastGmailRequest);
        Assert.Equal("client-secret-from-reference", fixture.ProfileAuthService.LastGmailRequest!.ClientSecret);
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
                "--value-stdin",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder(),
            new StringReader("super-secret"));

        var secretValue = await fixture.SecretStore.GetSecretAsync("work-imap", MailSecretNames.Password);

        Assert.Equal(0, exitCode);
        Assert.Equal("super-secret", secretValue);
    }

    [Fact]
    public async Task ProfileSetSecretSupportsReferenceCopy() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var fixture = CreateFixture();
        await fixture.ProfileStore.SaveAsync(new MailProfile {
            Id = "shared-secrets",
            DisplayName = "Shared Secrets",
            Kind = MailProfileKind.Gmail,
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                [MailProfileSettingsKeys.Mailbox] = "shared@example.com",
                [MailProfileSettingsKeys.ClientId] = "shared-client"
            }
        });
        await fixture.SecretStore.SetSecretAsync("shared-secrets", MailSecretNames.Password, "copied-secret");

        var exitCode = await CliRunner.RunAsync(
            new[] {
                "profile", "set-secret",
                "--profile", "work-imap",
                "--name", MailSecretNames.Password,
                "--value-ref", $"shared-secrets:{MailSecretNames.Password}",
                "--json"
            },
            stdout,
            stderr,
            _ => fixture.CreateBuilder());

        var secretValue = await fixture.SecretStore.GetSecretAsync("work-imap", MailSecretNames.Password);

        Assert.Equal(0, exitCode);
        Assert.Equal("copied-secret", secretValue);
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
}
#endif
