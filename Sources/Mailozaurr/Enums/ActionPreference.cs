namespace Mailozaurr;

/// <summary>
/// Specifies how the client should react when an error occurs.
/// </summary>
public enum ActionPreference {
    /// <summary>
    /// Stop execution when an error occurs.
    /// </summary>
    Stop,

    /// <summary>
    /// Continue execution regardless of errors.
    /// </summary>
    Continue,

    /// <summary>
    /// Ask the user whether to continue when an error occurs.
    /// </summary>
    Inquire,

    /// <summary>
    /// Ignore errors and continue without notification.
    /// </summary>
    SilentlyContinue,

    /// <summary>
    /// Pause execution for further action.
    /// </summary>
    Suspend
}
