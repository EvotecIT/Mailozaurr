namespace Mailozaurr;

/// <summary>
/// Truthful, non-secret evidence collected while testing a mail profile.
/// </summary>
public sealed class MailProfileDiagnosticEvidence {
    /// <summary>Provider or wire protocol that produced the evidence.</summary>
    public string? Protocol { get; set; }

    /// <summary>Observed authenticated-session state, when the provider exposes it.</summary>
    public MailProfileSessionEvidence? Session { get; set; }

    /// <summary>Provider-verified account identity, when queried successfully.</summary>
    public MailProfileIdentityEvidence? Identity { get; set; }

    /// <summary>Why identity evidence is unavailable even though another provider capability was verified.</summary>
    public string? IdentityUnavailableReason { get; set; }

    /// <summary>Permission claims visible to the client and their authority boundary.</summary>
    public MailProfilePermissionEvidence? Permissions { get; set; }

    /// <summary>Mailbox state observed by the requested probe.</summary>
    public MailProfileMailboxEvidence? Mailbox { get; set; }

    /// <summary>Non-destructive readiness evidence for an intended operation.</summary>
    public MailProfilePreflightEvidence? Preflight { get; set; }
}

/// <summary>Observed protocol-session facts.</summary>
public sealed class MailProfileSessionEvidence {
    /// <summary>Whether the client observed an active connection.</summary>
    public bool? Connected { get; set; }

    /// <summary>Whether the client observed an authenticated session.</summary>
    public bool? Authenticated { get; set; }

    /// <summary>Whether the active connection is protected by TLS.</summary>
    public bool? Secure { get; set; }

    /// <summary>Negotiated TLS protocol, when exposed by the provider client.</summary>
    public string? TlsProtocol { get; set; }

    /// <summary>Server capabilities observed after connection/authentication.</summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>Authentication mechanisms advertised by the server.</summary>
    public List<string> AuthenticationMechanisms { get; set; } = new();
}

/// <summary>Provider-verified account identity.</summary>
public sealed class MailProfileIdentityEvidence {
    /// <summary>Stable provider account identifier, when available.</summary>
    public string? Id { get; set; }

    /// <summary>Provider-reported primary email or user principal name.</summary>
    public string? EmailAddress { get; set; }

    /// <summary>Provider-reported display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Identity endpoint or provider surface that supplied the value.</summary>
    public string? Source { get; set; }
}

/// <summary>Permission evidence and its authority boundary.</summary>
public sealed class MailProfilePermissionEvidence {
    /// <summary>Permission or role names visible to the client.</summary>
    public List<string> Names { get; set; } = new();

    /// <summary>Where the permission names came from.</summary>
    public string? Source { get; set; }

    /// <summary>Whether the provider authoritatively confirmed the effective permissions.</summary>
    public bool Authoritative { get; set; }

    /// <summary>Explanation of limits on the reported permission evidence.</summary>
    public string? Detail { get; set; }
}

/// <summary>Mailbox state observed by a diagnostic probe.</summary>
public sealed class MailProfileMailboxEvidence {
    /// <summary>Provider-reported total message count, when available.</summary>
    public long? MessageCount { get; set; }

    /// <summary>Provider-reported unread message count, when available.</summary>
    public long? UnreadCount { get; set; }

    /// <summary>Provider-reported thread count, when available.</summary>
    public long? ThreadCount { get; set; }

    /// <summary>Number of folders or labels observed by the bounded probe.</summary>
    public int? FolderCount { get; set; }

    /// <summary>Provider change cursor such as Gmail history id or IMAP UID validity.</summary>
    public string? ChangeCursor { get; set; }
}

/// <summary>Non-destructive evidence about readiness for an operation.</summary>
public sealed class MailProfilePreflightEvidence {
    /// <summary>Operation whose readiness was evaluated.</summary>
    public string? Operation { get; set; }

    /// <summary>Depth of validation, for example session or provider-endpoint.</summary>
    public string? ValidationLevel { get; set; }

    /// <summary>Whether the observed evidence supports attempting the operation.</summary>
    public bool? Ready { get; set; }

    /// <summary>Explicit statement of what the preflight did and did not prove.</summary>
    public string? Detail { get; set; }
}
