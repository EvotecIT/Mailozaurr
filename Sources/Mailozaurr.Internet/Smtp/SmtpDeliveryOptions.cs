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

    /// <summary>Validates the delivery policy and resolves endpoint-specific security overrides.</summary>
    internal SmtpEndpoint[] ResolveEndpoints(MailKit.Security.SecureSocketOptions defaults) {
        if (Servers == null || Servers.Count == 0) throw new ArgumentException("At least one SMTP endpoint is required.", nameof(Servers));
        if (MaxAttempts < 1 || CircuitBreakMinutes < 0 || !Enum.IsDefined(typeof(SmtpServerStrategy), Strategy)) {
            throw new ArgumentOutOfRangeException(nameof(SmtpDeliveryOptions), "The SMTP strategy, attempt budget and cooldown must be valid.");
        }
        return Servers.Select(endpoint => {
            if (endpoint == null) throw new ArgumentException("An SMTP endpoint cannot be null.", nameof(Servers));
            return SmtpEndpoint.Parse(endpoint.Server, endpoint.Port, endpoint.SecureSocketOptions ?? defaults);
        }).ToArray();
    }
}
