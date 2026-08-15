namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Top level DSN status classes as defined in RFC 3463.
/// </summary>
public enum DsnStatusClass {
    /// <summary>Success (2.x.x).</summary>
    Success = 2,
    /// <summary>Persistent transient failure (4.x.x).</summary>
    PersistentTransientFailure = 4,
    /// <summary>Permanent failure (5.x.x).</summary>
    PermanentFailure = 5
}