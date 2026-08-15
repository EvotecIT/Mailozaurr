using System.Threading;

using System.Threading.Tasks;



namespace Mailozaurr;



/// <summary>Provides a mechanism to send pending messages using provider-specific transports.</summary>

public interface IPendingMessageSender {

    /// <summary>Sends the pending message.</summary>
    /// <param name="record">The pending message metadata.</param>
    /// <param name="ct">Token used to observe cancellation requests.</param>

    Task SendAsync(PendingMessageRecord record, CancellationToken ct);

}