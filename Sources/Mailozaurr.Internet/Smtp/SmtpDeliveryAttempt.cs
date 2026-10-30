namespace Mailozaurr;

/// <summary>Outcome of a single host attempt; numbering starts at one for each message.</summary>
public sealed class SmtpDeliveryAttempt {
    /// <summary>SMTP host attempted.</summary>
    public string Server { get; init; } = string.Empty;
    /// <summary>TCP port attempted.</summary>
    public int Port { get; init; }
    /// <summary>Global attempt number across hosts.</summary>
    public int Number { get; init; }
    /// <summary>Whether the attempt succeeded or delivery was accepted.</summary>
    public bool Succeeded { get; init; }
    /// <summary>Whether a retryable transport or SMTP failure occurred.</summary>
    public bool IsTransient { get; init; }
    /// <summary>Failure details, if any.</summary>
    public string? Error { get; init; }
    /// <summary>Whether transport loss during sending prevented confirmation of acceptance.</summary>
    public bool DeliveryUnknown { get; init; }
}
