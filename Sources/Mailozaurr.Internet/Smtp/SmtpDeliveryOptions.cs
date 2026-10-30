namespace Mailozaurr;

/// <summary>Host selection and bounded retry policy for a delivery session.</summary>
public sealed class SmtpDeliveryOptions {
    /// <summary>Ordered SMTP endpoints. At least one is required.</summary>
    public List<SmtpEndpoint> Servers { get; set; } = new();
    /// <summary>Starting-host strategy. Transient failures advance to the next eligible host.</summary>
    public SmtpServerStrategy Strategy { get; set; } = SmtpServerStrategy.Failover;
    /// <summary>Total transport attempts per message across all hosts, including the first.</summary>
    public int MaxAttempts { get; set; } = 4;
    /// <summary>Minutes to exclude a host following a transient failure. Zero disables cooldown.</summary>
    public int CircuitBreakMinutes { get; set; } = 2;
}
