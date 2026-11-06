namespace Mailozaurr;

/// <summary>Delivery evidence for one profile attempt.</summary>
public enum MailDeliveryOutcome {
    /// <summary>The provider confirmed acceptance.</summary>
    Accepted,
    /// <summary>The provider did not accept the message and the route may try another profile.</summary>
    SafeToFallback,
    /// <summary>Acceptance was not confirmed. The route stops without resubmitting.</summary>
    Unconfirmed
}

/// <summary>One profile attempt, with a safe diagnostic code and no credential or message data.</summary>
/// <param name="ProfileId">Configured transport profile ID.</param>
/// <param name="Kind">Transport used by the profile.</param>
/// <param name="Outcome">What was established about provider acceptance.</param>
/// <param name="Code">Stable diagnostic code for a failed attempt.</param>
/// <param name="ProviderMessageId">Provider message ID when available.</param>
public sealed record MailDeliveryAttempt(string ProfileId, MailProfileKind Kind, MailDeliveryOutcome Outcome,
    string? Code = null, string? ProviderMessageId = null);

/// <summary>Ordered attempt history and the outcome of a route invocation.</summary>
/// <param name="RouteId">Configured route ID.</param>
/// <param name="Attempts">Profile attempts in delivery order.</param>
public sealed record MailDeliveryResult(string RouteId, IReadOnlyList<MailDeliveryAttempt> Attempts) {
    /// <summary>Whether the final provider confirmed acceptance.</summary>
    public bool Accepted => Attempts.Count > 0 && Attempts[Attempts.Count - 1].Outcome == MailDeliveryOutcome.Accepted;
}
