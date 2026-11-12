namespace Mailozaurr;

/// <summary>Signals that SMTP submission lost confirmation and must be reconciled before resending.</summary>
/// <remarks>Custom <see cref="IPendingMessageSender"/> implementations must throw this exception when a failed or cancelled submission may have been accepted. Failures known to occur before submission can retain their original exception type.</remarks>
public sealed class SmtpDeliveryUnknownException : IOException {
    /// <summary>Creates an unknown-delivery signal retaining the transport or cancellation failure.</summary>
    /// <param name="innerException">The failure that prevented confirmation.</param>
    public SmtpDeliveryUnknownException(Exception innerException)
        : base("SMTP submission lost confirmation; do not resubmit without reconciling delivery.", innerException) { }
}
