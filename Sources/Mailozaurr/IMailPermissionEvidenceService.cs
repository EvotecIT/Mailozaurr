namespace Mailozaurr;

/// <summary>Collects truthful provider permission evidence without claiming mailbox delegation management.</summary>
public interface IMailPermissionEvidenceService {
    /// <summary>Runs the bounded provider mailbox probe and returns its permission evidence.</summary>
    Task<MailPermissionEvidenceResult> GetEvidenceAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default);
}

/// <summary>Permission evidence collected for one profile.</summary>
public sealed class MailPermissionEvidenceResult {
    /// <summary>Profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Provider kind.</summary>
    public MailProfileKind Provider { get; set; }
    /// <summary>Whether the bounded provider probe succeeded.</summary>
    public bool ProbeSucceeded { get; set; }
    /// <summary>Provider identity established by the probe, when available.</summary>
    public MailProfileIdentityEvidence? Identity { get; set; }
    /// <summary>Permission claims and their authority boundary.</summary>
    public MailProfilePermissionEvidence? Permissions { get; set; }
    /// <summary>Failure code when the probe failed.</summary>
    public string? FailureCode { get; set; }
    /// <summary>Human-readable probe outcome.</summary>
    public string? Message { get; set; }
}
