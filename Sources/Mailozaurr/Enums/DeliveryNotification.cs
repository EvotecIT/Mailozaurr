namespace Mailozaurr;

/// <summary>
/// Delivery status notification options.
/// </summary>
/// <remarks>
/// These values map directly to SMTP <c>NOTIFY</c> settings when
/// requesting delivery status notifications from the server.
/// </remarks>
public enum DeliveryNotification {
    /// <summary>
    /// No delivery status notifications are requested.
    /// </summary>
    None,

    /// <summary>
    /// Notify when delivery is delayed.
    /// </summary>
    Delay,

    /// <summary>
    /// Suppress all delivery notifications.
    /// </summary>
    Never,

    /// <summary>
    /// Notify on delivery failure.
    /// </summary>
    OnFailure,

    /// <summary>
    /// Notify on successful delivery.
    /// </summary>
    OnSuccess,
}