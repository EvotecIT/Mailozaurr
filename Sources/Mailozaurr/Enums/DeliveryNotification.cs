namespace Mailozaurr;

/// <summary>
/// Delivery status notification options.
/// </summary>
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
