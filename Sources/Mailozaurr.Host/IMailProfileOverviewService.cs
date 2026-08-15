namespace Mailozaurr.Hosting;

/// <summary>
/// Produces reusable high-level summaries for saved profiles.
/// </summary>
public interface IMailProfileOverviewService {
    /// <summary>Builds summary views for all saved profiles.</summary>
    Task<IReadOnlyList<MailProfileOverview>> GetOverviewsAsync(
        MailProfileOverviewQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>Builds lightweight summary views for all saved profiles.</summary>
    Task<IReadOnlyList<MailProfileOverviewCompact>> GetCompactOverviewsAsync(
        MailProfileOverviewQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>Builds a summary view for a saved profile.</summary>
    Task<MailProfileOverview?> GetOverviewAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Builds a lightweight summary view for a saved profile.</summary>
    Task<MailProfileOverviewCompact?> GetCompactOverviewAsync(string profileId, CancellationToken cancellationToken = default);
}