using System.Threading;

using System.Threading.Tasks;



namespace Mailozaurr;



/// <summary>Provides a no-operation implementation for queued messages without a provider.</summary>

internal sealed class NoopPendingMessageSender : IPendingMessageSender {

    internal static NoopPendingMessageSender Instance { get; } = new();



    private NoopPendingMessageSender() {

    }



    public Task SendAsync(PendingMessageRecord record, CancellationToken ct) => Task.CompletedTask;

}

