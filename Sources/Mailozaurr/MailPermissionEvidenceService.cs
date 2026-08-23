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
        if (!profile.GetCapabilities().Supports(MailCapability.InspectPermissions)) {
            throw new NotSupportedException($"Profile '{profile.Id}' does not allow '{MailCapability.InspectPermissions}'.");
        }

        var effective = ProviderMailboxProfileResolver.WithMailbox(profile, mailboxId);
        MailProfileDiagnosticEvidence evidence;
        try {
            if (profile.Kind == MailProfileKind.Graph) {
                using var session = await _graphSessionFactory.ConnectAsync(effective, cancellationToken).ConfigureAwait(false);
                evidence = MailProfileDiagnosticEvidenceFactory.CreateGraphEvidence(session);
                await MailProfileDiagnosticEvidenceFactory.AttachGraphIdentityAsync(session, evidence, cancellationToken).ConfigureAwait(false);
            } else {
                using var session = await _gmailSessionFactory.ConnectAsync(effective, cancellationToken).ConfigureAwait(false);
                try {
                    var mailbox = await session.Browser.GetProfileWithoutRefreshAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(mailbox.EmailAddress)) {
                        return Failure(profile, "gmail_identity_missing", "Gmail users.getProfile returned no email address.");
                    }
                    evidence = MailProfileDiagnosticEvidenceFactory.CreateGmailEvidence(mailbox);
                } catch (GmailAuthenticationException ex) when (MailProfileDiagnosticEvidenceFactory.IsGmailInsufficientScope(ex)) {
                    evidence = MailProfileDiagnosticEvidenceFactory.CreateGmailCapabilityEvidence(
                        "Gmail recognized the credential but users.getProfile was outside its granted scope. OAuth scope metadata was not available and was not inferred.");
                    evidence.IdentityUnavailableReason = "Gmail users.getProfile returned 403 Forbidden for this credential scope.";
                }
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
