namespace Mailozaurr.Hosting;

/// <summary>
/// Executes one or more normalized message action plans and returns a shared summary.
/// </summary>
public interface IMailMessageActionBatchService {
    /// <summary>Executes a batch of normalized message action plans.</summary>
    Task<MessageActionBatchExecutionResult> ExecuteAsync(
        IReadOnlyList<MessageActionExecutionPlan> plans,
        bool continueOnError = true,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? confirmationTokens = null);
}
