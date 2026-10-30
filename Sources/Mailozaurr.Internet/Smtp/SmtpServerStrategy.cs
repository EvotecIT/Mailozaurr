namespace Mailozaurr;

/// <summary>Determines the first eligible SMTP host used for each message.</summary>
public enum SmtpServerStrategy {
    /// <summary>Prefer the first configured host, then fail over in order.</summary>
    Failover,
    /// <summary>Advance the starting host for each message.</summary>
    RoundRobin,
    /// <summary>Choose a random starting host, then fail over in order.</summary>
    Random
}
