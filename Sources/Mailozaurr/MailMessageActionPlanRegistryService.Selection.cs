namespace Mailozaurr;

public sealed partial class MailMessageActionPlanRegistryService {
    private static IReadOnlyList<MessageActionPreviewItem> SelectCommonActions(
        CommonMessageActionsPreview preview,
        IReadOnlyList<string>? actions) {
        if (actions == null || actions.Count == 0) {
            return preview.Actions
                .Where(action => action.Succeeded)
                .ToArray();
        }

        var selected = new List<MessageActionPreviewItem>();
        foreach (var actionName in actions
                     .Where(action => !string.IsNullOrWhiteSpace(action))
                     .Select(action => action.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)) {
            var match = preview.Actions.FirstOrDefault(action =>
                string.Equals(action.Action, actionName, StringComparison.OrdinalIgnoreCase) &&
                action.Succeeded);
            if (match != null) {
                selected.Add(match);
            }
        }

        return selected;
    }

    private static string? ResolveDestinationFolderId(
        MessageActionPreviewItem action,
        string? fallbackDestinationFolderId) =>
        string.Equals(action.Action, "move", StringComparison.OrdinalIgnoreCase)
            ? action.Destination?.EffectiveFolderId ?? action.RequestedDestinationFolderId ?? fallbackDestinationFolderId
            : null;

    private static IReadOnlyList<SelectedPlan> SelectPlans(
        MailMessageActionPlanBatch source,
        MessageActionPlanBatchTransformRequest transform,
        out OperationResult? error) {
        error = null;

        var selected = new Dictionary<int, SelectedPlan>();
        var hasIndexes = transform.PlanIndexes != null && transform.PlanIndexes.Count > 0;
        var hasNames = transform.PlanNames != null && transform.PlanNames.Count > 0;
        IEnumerable<int> requestedIndexes = hasIndexes ? transform.PlanIndexes! : Array.Empty<int>();
        IEnumerable<string> requestedNames = hasNames ? transform.PlanNames! : Array.Empty<string>();

        if (!hasIndexes && !hasNames) {
            return source.Plans
                .Select((plan, index) => new SelectedPlan(index, plan))
                .ToArray();
        }

        if (hasIndexes) {
            foreach (var index in requestedIndexes.Distinct()) {
                if (index < 0 || index >= source.Plans.Count) {
                    error = OperationResult.Failure(
                        "action_plan_batch_index_invalid",
                        $"Action plan batch '{source.Id}' does not contain a plan at index {index}.");
                    return Array.Empty<SelectedPlan>();
                }

                selected[index] = new SelectedPlan(index, source.Plans[index]);
            }
        }

        if (hasNames) {
            foreach (var requestedName in requestedNames
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Select(name => name.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)) {
                var matches = source.Plans
                    .Select((plan, index) => new SelectedPlan(index, plan))
                    .Where(selection => PlanMatchesName(selection.Plan, requestedName))
                    .ToArray();
                if (matches.Length == 0) {
                    error = OperationResult.Failure(
                        "action_plan_batch_name_invalid",
                        $"Action plan batch '{source.Id}' does not contain a plan named '{requestedName}'.");
                    return Array.Empty<SelectedPlan>();
                }

                foreach (var match in matches) {
                    selected[match.Index] = match;
                }
            }
        }

        if (selected.Count == 0) {
            error = OperationResult.Failure(
                "action_plan_batch_invalid",
                "Action plan batch transform must include at least one plan.");
            return Array.Empty<SelectedPlan>();
        }

        return selected
            .OrderBy(item => item.Key)
            .Select(item => item.Value)
            .ToArray();
    }

    private static bool PlanMatchesName(MessageActionExecutionPlan plan, string requestedName) =>
        (!string.IsNullOrWhiteSpace(plan.Name) &&
         string.Equals(plan.Name, requestedName, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(plan.Summary) &&
         string.Equals(plan.Summary, requestedName, StringComparison.OrdinalIgnoreCase));

    private sealed class SelectedPlan {
        internal SelectedPlan(int index, MessageActionExecutionPlan plan) {
            Index = index;
            Plan = plan;
        }

        internal int Index { get; }

        internal MessageActionExecutionPlan Plan { get; }
    }
}
