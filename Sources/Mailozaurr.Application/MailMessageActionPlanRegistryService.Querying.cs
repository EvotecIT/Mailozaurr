namespace Mailozaurr.Application;

public sealed partial class MailMessageActionPlanRegistryService {
    private static IReadOnlyList<MailMessageActionPlanBatch> ApplyBatchQuery(
        IReadOnlyList<MailMessageActionPlanBatch> batches,
        MailMessageActionPlanBatchQuery? query) {
        if (query == null) {
            return batches;
        }

        var requestedPlanNames = query.PlanNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedProfileIds = query.ProfileIds
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Select(profileId => profileId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedActions = query.Actions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IEnumerable<MailMessageActionPlanBatch> filtered = batches;
        if (requestedPlanNames.Length > 0 || requestedProfileIds.Length > 0 || requestedActions.Length > 0) {
            filtered = filtered.Where(batch =>
                (requestedPlanNames.Length == 0 || batch.Plans.Any(plan =>
                    requestedPlanNames.Any(requestedName => PlanMatchesName(plan, requestedName)))) &&
                (requestedProfileIds.Length == 0 || batch.Plans.Any(plan =>
                    requestedProfileIds.Any(requestedProfileId => string.Equals(
                        plan.ProfileId,
                        requestedProfileId,
                        StringComparison.OrdinalIgnoreCase)))) &&
                (requestedActions.Length == 0 || batch.Plans.Any(plan =>
                    requestedActions.Any(requestedAction => string.Equals(
                        plan.Action,
                        requestedAction,
                        StringComparison.OrdinalIgnoreCase)))));
        }

        var ordered = ApplyBatchQuerySort(filtered, query.SortBy, query.Descending);
        return ordered.ToArray();
    }

    private static IEnumerable<MailMessageActionPlanBatch> ApplyBatchQuerySort(
        IEnumerable<MailMessageActionPlanBatch> batches,
        MailMessageActionPlanBatchSortBy sortBy,
        bool descending) {
        Func<MailMessageActionPlanBatch, object> keySelector = sortBy switch {
            MailMessageActionPlanBatchSortBy.Name => batch => batch.Name,
            MailMessageActionPlanBatchSortBy.PlanCount => batch => batch.Plans.Count,
            MailMessageActionPlanBatchSortBy.ReadyPlanCount => batch => batch.Plans.Count(plan => plan.Succeeded),
            MailMessageActionPlanBatchSortBy.UpdatedAt => batch => batch.UpdatedAt,
            MailMessageActionPlanBatchSortBy.ActionTypeCount => batch => batch.Plans
                .Select(plan => plan.Action)
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            _ => batch => batch.Id
        };

        var ordered = descending
            ? batches.OrderByDescending(keySelector)
            : batches.OrderBy(keySelector);

        return ordered.ThenBy(batch => batch.Id, StringComparer.OrdinalIgnoreCase);
    }
}
