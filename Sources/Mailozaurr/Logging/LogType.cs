namespace Mailozaurr;

/// <summary>
/// Represents the type of a log entry for use in cmdlet and client logging.
/// </summary>
/// <remarks>
/// Each value corresponds to a message stream exposed by the
/// PowerShell module when executing operations.
/// </remarks>
public enum LogType {
    /// <summary>A warning message.</summary>
    Warning,
    /// <summary>An error message.</summary>
    Error,
    /// <summary>A verbose message.</summary>
    Verbose,
    /// <summary>An informational message.</summary>
    Information
}
