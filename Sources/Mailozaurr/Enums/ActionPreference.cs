namespace Mailozaurr;

/// <summary>
/// Specifies how the client should react when an error occurs.
/// </summary>
public enum ActionPreference {
    Stop,
    Continue,
    Inquire,
    SilentlyContinue,
    Suspend
}
