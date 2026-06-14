namespace Mailozaurr.Application;

/// <summary>
/// Supported sort keys for bulk profile overview queries.
/// </summary>
public enum MailProfileOverviewSortBy {
    /// <summary>Sort by profile identifier.</summary>
    Id = 0,

    /// <summary>Sort by profile kind, then identifier.</summary>
    Kind = 1,

    /// <summary>Sort by readiness, with profiles needing attention first.</summary>
    Readiness = 2,
}