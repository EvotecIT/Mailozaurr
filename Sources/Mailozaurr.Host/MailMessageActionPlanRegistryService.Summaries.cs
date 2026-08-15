namespace Mailozaurr.Hosting;

public sealed partial class MailMessageActionPlanRegistryService {
    private static MailMessageActionPlanBatchCompact ToCompact(MailMessageActionPlanBatch batch) {
        var profileCount = batch.Plans
            .Select(plan => plan.ProfileId)
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var readyPlanCount = batch.Plans.Count(plan => plan.Succeeded);

        return new MailMessageActionPlanBatchCompact {
            Id = batch.Id,
            Name = batch.Name,
            PlanCount = batch.Plans.Count,
            ReadyPlanCount = readyPlanCount,
            ProfileCount = profileCount,
            PlanNames = batch.Plans
                .Select(plan => !string.IsNullOrWhiteSpace(plan.Name) ? plan.Name : BuildStoredPlanSummary(plan))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UpdatedAt = batch.UpdatedAt,
            Summary = $"{batch.Id} ({batch.Plans.Count} plan(s), {readyPlanCount} ready)"
        };
    }

    private static MailMessageActionPlanBatchSummary ToSummary(MailMessageActionPlanBatch batch) {
        var profileIds = batch.Plans
            .Select(plan => plan.ProfileId)
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(profileId => profileId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var readyPlanCount = batch.Plans.Count(plan => plan.Succeeded);
        var actionCounts = batch.Plans
            .Where(plan => !string.IsNullOrWhiteSpace(plan.Action))
            .GroupBy(plan => plan.Action, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new MailMessageActionPlanBatchSummary {
            Id = batch.Id,
            Name = batch.Name,
            PlanCount = batch.Plans.Count,
            ReadyPlanCount = readyPlanCount,
            ProfileIds = profileIds,
            ActionCounts = actionCounts,
            PlanNames = batch.Plans
                .Select(plan => !string.IsNullOrWhiteSpace(plan.Name) ? plan.Name : BuildStoredPlanSummary(plan))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UpdatedAt = batch.UpdatedAt,
            Summary = $"{batch.Id} ({batch.Plans.Count} plan(s), {readyPlanCount} ready, {actionCounts.Count} action type(s))"
        };
    }

    private static string BuildStoredPlanSummary(MessageActionExecutionPlan plan) {
        var messageCount = plan.UniqueMessageCount == 1 ? "1 message" : $"{plan.UniqueMessageCount} messages";
        return !string.IsNullOrWhiteSpace(plan.Name)
            ? $"{plan.Name} ({messageCount})"
            : $"{plan.Action} ({messageCount})";
    }
}
