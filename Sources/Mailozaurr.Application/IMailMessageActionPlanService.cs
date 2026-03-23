namespace Mailozaurr.Application;

/// <summary>
/// Creates reusable execution plans from previewable message actions and can execute those plans.
/// </summary>
public interface IMailMessageActionPlanService {
    /// <summary>Creates a normalized execution plan for a requested message action.</summary>
    Task<MessageActionExecutionPlan> CreatePlanAsync(MessageActionExecutionPlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>Executes a previously created normalized execution plan.</summary>
    Task<MessageActionResult> ExecuteAsync(MessageActionExecutionPlan plan, CancellationToken cancellationToken = default);
}
