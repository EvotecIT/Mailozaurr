namespace Mailozaurr.Application;

/// <summary>
/// Optional filters for bulk profile overview queries.
/// </summary>
public sealed class MailProfileOverviewQuery {
    /// <summary>Optional profile kind filter.</summary>
    public MailProfileKind? Kind { get; set; }

    /// <summary>Optional sort key for the returned overviews.</summary>
    public MailProfileOverviewSortBy SortBy { get; set; } = MailProfileOverviewSortBy.Id;

    /// <summary>When true, reverses the selected sort order.</summary>
    public bool Descending { get; set; }

    /// <summary>When true, only returns ready profiles.</summary>
    public bool ReadyOnly { get; set; }

    /// <summary>When true, only returns profiles that support reading.</summary>
    public bool CanReadOnly { get; set; }

    /// <summary>When true, only returns profiles that support sending.</summary>
    public bool CanSendOnly { get; set; }

    /// <summary>When true, only returns profiles marked as default.</summary>
    public bool DefaultOnly { get; set; }
}