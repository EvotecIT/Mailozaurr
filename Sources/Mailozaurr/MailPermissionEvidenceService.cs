namespace Mailozaurr;

/// <summary>Default permission-evidence service built on profile diagnostics v2.</summary>
public sealed class MailPermissionEvidenceService : IMailPermissionEvidenceService {
    private readonly IMailProfileStore _profileStore;
    private readonly IGraphSessionFactory _graphSessionFactory;
    private readonly IGmailSessionFactory _gmailSessionFactory;

    /// <summary>Creates a permission-evidence service.</summary>
    public MailPermissionEvidenceService(
        IMailProfileStore profileStore,
        IGraphSessionFactory graphSessionFactory,
        IGmailSessionFactory gmailSessionFactory) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _graphSessionFactory = graphSessionFactory ?? throw new ArgumentNullException(nameof(graphSessionFactory));
        _gmailSessionFactory = gmailSessionFactory ?? throw new ArgumentNullException(nameof(gmailSessionFactory));
    }

    /// <inheritdoc />
    public async Task<MailPermissionEvidenceResult> GetEvidenceAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Profile id is required.", nameof(profileId));
        var profile = await _profileStore.GetByIdAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");
        if (profile.Kind != MailProfileKind.Graph && profile.Kind != MailProfileKind.Gmail) {
            throw new NotSupportedException("OAuth permission evidence is currently available for Graph and Gmail profiles.");
        }

        var effective = ProviderMailboxProfileResolver.WithMailbox(profile, mailboxId);
        MailProfileDiagnosticEvidence evidence;
        try {
            if (profile.Kind == MailProfileKind.Graph) {
                using var session = await _graphSessionFactory.ConnectAsync(effective, cancellationToken).ConfigureAwait(false);
                evidence = MailProfileDiagnosticEvidenceFactory.CreateGraphEvidence(session);
                try {
                    var identity = await session.Client.GetMailboxIdentityWithoutRefreshAsync(session.UserId, cancellationToken).ConfigureAwait(false);
                    evidence = MailProfileDiagnosticEvidenceFactory.CreateGraphEvidence(session, identity);
                    MailProfileDiagnosticEvidenceFactory.ApplyVerifiedGraphDelegatedIdentity(evidence, identity);
                } catch (GraphApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden) {
                    evidence.IdentityUnavailableReason =
                        $"Microsoft Graph users endpoint returned {(int)ex.StatusCode} ({ex.StatusCode}); token-claim permission evidence remains available.";
                }
            } else {
                using var session = await _gmailSessionFactory.ConnectAsync(effective, cancellationToken).ConfigureAwait(false);
                var mailbox = await session.Browser.GetProfileAsync(cancellationToken).ConfigureAwait(false);
                evidence = MailProfileDiagnosticEvidenceFactory.CreateGmailEvidence(mailbox);
            }
        } catch (GraphApiException ex) {
            return Failure(profile, "graph_" + ((int)ex.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message);
        } catch (GmailAuthenticationException ex) {
            return Failure(profile, "gmail_" + ((int)ex.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message);
        } catch (GmailApiException ex) {
            var code = ex.StatusCode.HasValue
                ? "gmail_" + ((int)ex.StatusCode.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "gmail_invalid_response";
            return Failure(profile, code, ex.Message);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception) {
            return Failure(profile, "permission_evidence_failed", "Permission evidence could not be established.");
        }

        return new MailPermissionEvidenceResult {
            ProfileId = profile.Id,
            Provider = profile.Kind,
            ProbeSucceeded = true,
            Identity = evidence.Identity,
            Permissions = evidence.Permissions,
            Message = evidence.IdentityUnavailableReason ?? evidence.Permissions?.Detail
        };
    }

    private static MailPermissionEvidenceResult Failure(MailProfile profile, string code, string message) => new() {
        ProfileId = profile.Id,
        Provider = profile.Kind,
        ProbeSucceeded = false,
        FailureCode = code,
        Message = message
    };
}
