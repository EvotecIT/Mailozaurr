using System.Threading;

using System.Threading.Tasks;



namespace Mailozaurr;



/// <summary>Provides a mechanism to send pending messages using provider-specific transports.</summary>

public interface IPendingMessageSender {

    /// <summary>Sends the pending message.</summary>
    /// <param name="record">The pending message metadata.</param>
    /// <param name="ct">Token used to observe cancellation requests.</param>
    /// <exception cref="SmtpDeliveryUnknownException">The SMTP outcome is uncertain after submission, including cancellation while waiting for confirmation. The processor retains the delivery hold.</exception>

    Task SendAsync(PendingMessageRecord record, CancellationToken ct);

}
