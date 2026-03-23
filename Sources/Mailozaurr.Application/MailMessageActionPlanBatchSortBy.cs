namespace Mailozaurr.Application;

/// <summary>
/// Supported sort orders for stored action-plan batch queries.
/// </summary>
public enum MailMessageActionPlanBatchSortBy {
    /// <summary>Sort by stable batch identifier.</summary>
    Id,

    /// <summary>Sort by user-facing batch name.</summary>
    Name,

    /// <summary>Sort by total plan count.</summary>
    PlanCount,

    /// <summary>Sort by ready plan count.</summary>
    ReadyPlanCount,

    /// <summary>Sort by last updated timestamp.</summary>
    UpdatedAt,

    /// <summary>Sort by the number of distinct action types referenced by the batch.</summary>
    ActionTypeCount
}
