using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
    private static async Task<int> ExecuteProfileAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error,
        TextReader input) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing profile command. Use 'profile list', 'profile create', 'profile graph-bootstrap', 'profile gmail-bootstrap', 'profile graph-login', 'profile gmail-login', 'profile refresh-auth', 'profile auth-status', 'profile test', 'profile summary', 'profile capabilities', 'profile show', 'profile validate', 'profile doctor', 'profile delete', 'profile set-default', 'profile set-secret', or 'profile remove-secret'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        var json = parseResult.HasFlag("json");
        switch (subCommand) {
            case "list":
                if (parseResult.HasFlag("summary")) {
                    if (parseResult.HasFlag("compact")) {
                        var compactOverviews = await application.ProfileOverview.GetCompactOverviewsAsync(BuildProfileOverviewQuery(parseResult)).ConfigureAwait(false);
                        await WriteSequenceAsync(output, compactOverviews, json, value => value.Summary).ConfigureAwait(false);
                        return compactOverviews.All(value => value.IsReady) ? 0 : 1;
                    }
                    var overviews = await application.ProfileOverview.GetOverviewsAsync(BuildProfileOverviewQuery(parseResult)).ConfigureAwait(false);
                    await WriteSequenceAsync(output, overviews, json, value => value.Summary).ConfigureAwait(false);
                    return overviews.All(value => value.IsReady) ? 0 : 1;
                }
                var profiles = await application.Profiles.GetProfilesAsync().ConfigureAwait(false);
                await WriteSequenceAsync(output, profiles, json, profile => $"{profile.Id} [{profile.Kind}] {profile.DisplayName}").ConfigureAwait(false);
                return 0;
            case "create":
                var createdProfile = BuildProfile(parseResult);
                var createResult = await application.Profiles.SaveAsync(createdProfile).ConfigureAwait(false);
                await WriteItemAsync(output, createResult, json, value => value.Message ?? "Profile saved.").ConfigureAwait(false);
                return createResult.Succeeded ? 0 : 1;
            case "graph-bootstrap":
                ValidateSingleStdinSecretSource(parseResult, "client-secret", "access-token", "certificate-password");
                var graphBootstrapResult = await application.ProfileBootstrap.SaveGraphProfileAsync(new GraphProfileBootstrapRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    DisplayName = RequireOption(parseResult, "name"),
                    Description = parseResult.GetOption("description"),
                    Mailbox = RequireOption(parseResult, "mailbox"),
                    DefaultSender = parseResult.GetOption("default-sender"),
                    IsDefault = parseResult.HasFlag("is-default"),
                    ClientId = parseResult.GetOption("client-id"),
                    TenantId = parseResult.GetOption("tenant-id"),
                    ClientSecret = await ResolveSensitiveOptionAsync(parseResult, "client-secret", input).ConfigureAwait(false),
                    ClientSecretReference = parseResult.GetOption("client-secret-ref"),
                    AccessToken = await ResolveSensitiveOptionAsync(parseResult, "access-token", input).ConfigureAwait(false),
                    AccessTokenReference = parseResult.GetOption("access-token-ref"),
                    CertificatePath = parseResult.GetOption("certificate-path"),
                    CertificatePassword = await ResolveSensitiveOptionAsync(parseResult, "certificate-password", input).ConfigureAwait(false),
                    CertificatePasswordReference = parseResult.GetOption("certificate-password-ref")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, graphBootstrapResult, json, value => value.Message ?? "Graph profile saved.").ConfigureAwait(false);
                return graphBootstrapResult.Succeeded ? 0 : 1;
            case "gmail-bootstrap":
                ValidateSingleStdinSecretSource(parseResult, "client-secret", "refresh-token", "access-token");
                var gmailBootstrapResult = await application.ProfileBootstrap.SaveGmailProfileAsync(new GmailProfileBootstrapRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    DisplayName = RequireOption(parseResult, "name"),
                    Description = parseResult.GetOption("description"),
                    Mailbox = parseResult.GetOption("mailbox"),
                    DefaultSender = parseResult.GetOption("default-sender"),
                    IsDefault = parseResult.HasFlag("is-default"),
                    ClientId = parseResult.GetOption("client-id"),
                    ClientSecret = await ResolveSensitiveOptionAsync(parseResult, "client-secret", input).ConfigureAwait(false),
                    ClientSecretReference = parseResult.GetOption("client-secret-ref"),
                    RefreshToken = await ResolveSensitiveOptionAsync(parseResult, "refresh-token", input).ConfigureAwait(false),
                    RefreshTokenReference = parseResult.GetOption("refresh-token-ref"),
                    AccessToken = await ResolveSensitiveOptionAsync(parseResult, "access-token", input).ConfigureAwait(false),
                    AccessTokenReference = parseResult.GetOption("access-token-ref")
                }).ConfigureAwait(false);
                await WriteItemAsync(output, gmailBootstrapResult, json, value => value.Message ?? "Gmail profile saved.").ConfigureAwait(false);
                return gmailBootstrapResult.Succeeded ? 0 : 1;
            case "graph-login":
                var graphLoginResult = await application.ProfileAuth.LoginGraphAsync(new GraphProfileLoginRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    Login = parseResult.GetOption("login"),
                    Mailbox = parseResult.GetOption("mailbox"),
                    ClientId = parseResult.GetOption("client-id"),
                    TenantId = parseResult.GetOption("tenant-id"),
                    RedirectUri = parseResult.GetOption("redirect-uri"),
                    Scopes = parseResult.GetOptionValues("scope")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)
                        .ToArray()
                }).ConfigureAwait(false);
                await WriteItemAsync(output, graphLoginResult, json, value => value.Message ?? (value.Succeeded ? "Graph login completed." : "Graph login failed.")).ConfigureAwait(false);
                return graphLoginResult.Succeeded ? 0 : 1;
            case "gmail-login":
                var gmailLoginResult = await application.ProfileAuth.LoginGmailAsync(new GmailProfileLoginRequest {
                    ProfileId = RequireOption(parseResult, "profile"),
                    GmailAccount = parseResult.GetOption("mailbox"),
                    ClientId = parseResult.GetOption("client-id"),
                    ClientSecret = await ResolveSensitiveOptionAsync(parseResult, "client-secret", input).ConfigureAwait(false),
                    ClientSecretReference = parseResult.GetOption("client-secret-ref"),
                    Scopes = parseResult.GetOptionValues("scope")
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)
                        .ToArray()
                }).ConfigureAwait(false);
                await WriteItemAsync(output, gmailLoginResult, json, value => value.Message ?? (value.Succeeded ? "Gmail login completed." : "Gmail login failed.")).ConfigureAwait(false);
                return gmailLoginResult.Succeeded ? 0 : 1;
            case "refresh-auth":
                var refreshAuthResult = await application.ProfileAuth.RefreshAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, refreshAuthResult, json, value => value.Message ?? (value.Succeeded ? "Profile auth refreshed." : "Profile auth refresh failed.")).ConfigureAwait(false);
                return refreshAuthResult.Succeeded ? 0 : 1;
            case "auth-status":
                var authStatusProfileId = RequireOption(parseResult, "profile");
                var authStatus = await application.ProfileAuth.GetStatusAsync(authStatusProfileId).ConfigureAwait(false);
                if (authStatus == null) {
                    await error.WriteLineAsync($"Profile '{authStatusProfileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, authStatus, json, value => value.Summary).ConfigureAwait(false);
                return 0;
            case "test":
                var connectionResult = await application.ProfileConnections.TestAsync(
                    RequireOption(parseResult, "profile"),
                    ParseConnectionTestScope(parseResult.GetOption("scope"))).ConfigureAwait(false);
                await WriteItemAsync(output, connectionResult, json, value => value.Message ?? (value.Succeeded ? "Profile connection succeeded." : "Profile connection failed.")).ConfigureAwait(false);
                return connectionResult.Succeeded ? 0 : 1;
            case "summary":
                var summaryProfileId = RequireOption(parseResult, "profile");
                if (parseResult.HasFlag("compact")) {
                    var compactOverview = await application.ProfileOverview.GetCompactOverviewAsync(summaryProfileId).ConfigureAwait(false);
                    if (compactOverview == null) {
                        await error.WriteLineAsync($"Profile '{summaryProfileId}' was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactOverview, json, value => value.Summary).ConfigureAwait(false);
                    return compactOverview.IsReady ? 0 : 1;
                }
                var overview = await application.ProfileOverview.GetOverviewAsync(summaryProfileId).ConfigureAwait(false);
                if (overview == null) {
                    await error.WriteLineAsync($"Profile '{summaryProfileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, overview, json, value => value.Summary).ConfigureAwait(false);
                return overview.IsReady ? 0 : 1;
            case "show":
                var profileId = RequireOption(parseResult, "profile");
                var profile = await application.Profiles.GetProfileAsync(profileId).ConfigureAwait(false);
                if (profile == null) {
                    await error.WriteLineAsync($"Profile '{profileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, profile, json, p => $"{p.Id} [{p.Kind}] {p.DisplayName}").ConfigureAwait(false);
                return 0;
            case "capabilities":
                var capabilitiesProfileId = RequireOption(parseResult, "profile");
                var capabilities = await application.Profiles.GetCapabilitiesAsync(capabilitiesProfileId).ConfigureAwait(false);
                if (capabilities == null) {
                    await error.WriteLineAsync($"Profile '{capabilitiesProfileId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, capabilities, json, value => $"{value.Kind}: {value.Capabilities}").ConfigureAwait(false);
                return 0;
            case "validate":
                var validateId = RequireOption(parseResult, "profile");
                var profileToValidate = await application.Profiles.GetProfileAsync(validateId).ConfigureAwait(false);
                if (profileToValidate == null) {
                    await error.WriteLineAsync($"Profile '{validateId}' was not found.").ConfigureAwait(false);
                    return 1;
                }
                var result = await application.Profiles.ValidateAsync(profileToValidate).ConfigureAwait(false);
                await WriteItemAsync(output, result, json, r => r.Message ?? (r.Succeeded ? "Profile is valid." : "Profile is invalid.")).ConfigureAwait(false);
                return result.Succeeded ? 0 : 1;
            case "doctor":
                var doctorResult = await application.Profiles.DiagnoseAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, doctorResult, json, value => value.Message ?? (value.Succeeded ? "Profile is ready." : "Profile is not ready.")).ConfigureAwait(false);
                return doctorResult.Succeeded ? 0 : 1;
            case "delete":
                var deleteResult = await application.Profiles.DeleteAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, deleteResult, json, value => value.Message ?? "Profile deleted.").ConfigureAwait(false);
                return deleteResult.Succeeded ? 0 : 1;
            case "set-default":
                var setDefaultResult = await application.Profiles.SetDefaultAsync(RequireOption(parseResult, "profile")).ConfigureAwait(false);
                await WriteItemAsync(output, setDefaultResult, json, value => value.Message ?? "Default profile updated.").ConfigureAwait(false);
                return setDefaultResult.Succeeded ? 0 : 1;
            case "set-secret":
                var secretValue = await ResolveSensitiveOptionAsync(parseResult, "value", input).ConfigureAwait(false);
                var secretReference = parseResult.GetOption("value-ref");
                if (secretValue == null && string.IsNullOrWhiteSpace(secretReference)) {
                    throw new InvalidOperationException(
                        "Missing required secret source. Use '--value-env <name>', '--value-stdin', or '--value-ref <profile-id:secret-name>'.");
                }
                var setSecretResult = await application.ProfileSecrets.SetSecretAsync(
                    RequireOption(parseResult, "profile"),
                    RequireOption(parseResult, "name"),
                    secretValue,
                    secretReference).ConfigureAwait(false);
                await WriteItemAsync(output, setSecretResult, json, value => value.Message ?? "Secret saved.").ConfigureAwait(false);
                return setSecretResult.Succeeded ? 0 : 1;
            case "remove-secret":
                var removeSecretResult = await application.ProfileSecrets.RemoveSecretAsync(
                    RequireOption(parseResult, "profile"),
                    RequireOption(parseResult, "name")).ConfigureAwait(false);
                await WriteItemAsync(output, removeSecretResult, json, value => value.Message ?? "Secret removed.").ConfigureAwait(false);
                return removeSecretResult.Succeeded ? 0 : 1;
            default:
                await error.WriteLineAsync($"Unknown profile command '{subCommand}'.").ConfigureAwait(false);
                return 1;
        }
    }
}
