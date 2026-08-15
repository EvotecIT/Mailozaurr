namespace Mailozaurr.Hosting;

/// <summary>
/// Default implementation of reusable profile overview aggregation.
/// </summary>
public sealed class MailProfileOverviewService : IMailProfileOverviewService {
    private readonly IMailProfileService _profiles;
    private readonly IMailProfileAuthService _profileAuth;

    /// <summary>
    /// Creates a new overview service.
    /// </summary>
    public MailProfileOverviewService(IMailProfileService profiles, IMailProfileAuthService profileAuth) {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _profileAuth = profileAuth ?? throw new ArgumentNullException(nameof(profileAuth));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailProfileOverview>> GetOverviewsAsync(
        MailProfileOverviewQuery? query = null,
        CancellationToken cancellationToken = default) {
        var profiles = await _profiles.GetProfilesAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<MailProfileOverview>(profiles.Count);
        foreach (var profile in profiles) {
            if (!MatchesProfileFilter(profile, query)) {
                continue;
            }

            var overview = await BuildOverviewAsync(profile, cancellationToken).ConfigureAwait(false);
            if (!MatchesOverviewFilter(overview, query)) {
                continue;
            }

            results.Add(overview);
        }

        return SortOverviews(results, query);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailProfileOverviewCompact>> GetCompactOverviewsAsync(
        MailProfileOverviewQuery? query = null,
        CancellationToken cancellationToken = default) =>
        (await GetOverviewsAsync(query, cancellationToken).ConfigureAwait(false))
        .Select(ToCompact)
        .ToArray();

    /// <inheritdoc />
    public async Task<MailProfileOverview?> GetOverviewAsync(string profileId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        var profile = await _profiles.GetProfileAsync(profileId.Trim(), cancellationToken).ConfigureAwait(false);
        if (profile == null) {
            return null;
        }

        return await BuildOverviewAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MailProfileOverviewCompact?> GetCompactOverviewAsync(string profileId, CancellationToken cancellationToken = default) {
        var overview = await GetOverviewAsync(profileId, cancellationToken).ConfigureAwait(false);
        return overview == null ? null : ToCompact(overview);
    }

    private async Task<MailProfileOverview> BuildOverviewAsync(MailProfile profile, CancellationToken cancellationToken) {
        var capabilities = await _profiles.GetCapabilitiesAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        var authStatus = await _profileAuth.GetStatusAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        var readiness = await _profiles.DiagnoseAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        var supportsRead = capabilities?.Supports(MailCapability.SearchMessages | MailCapability.ReadMessages) == true;
        var supportsSend = capabilities?.Supports(MailCapability.SendMessages) == true;

        return new MailProfileOverview {
            Profile = profile,
            Capabilities = capabilities,
            SupportsRead = supportsRead,
            SupportsSend = supportsSend,
            AuthStatus = authStatus,
            Readiness = readiness,
            IsReady = readiness.Succeeded,
            ErrorCount = readiness.Errors.Count,
            WarningCount = readiness.Warnings.Count,
            Summary = BuildSummary(profile, authStatus, readiness, supportsRead, supportsSend)
        };
    }

    private static string BuildSummary(
        MailProfile profile,
        MailProfileAuthStatus? authStatus,
        MailProfileValidationResult readiness,
        bool supportsRead,
        bool supportsSend) {
        var authMode = authStatus?.Mode ?? "unknown";
        var readinessState = readiness.Succeeded ? "ready" : "needs-attention";
        var readState = supportsRead ? "yes" : "no";
        var sendState = supportsSend ? "yes" : "no";
        var warningSuffix = readiness.Warnings.Count > 0 ? $", warnings={readiness.Warnings.Count}" : string.Empty;
        var errorSuffix = readiness.Errors.Count > 0 ? $", errors={readiness.Errors.Count}" : string.Empty;
        return $"{profile.Id} [{profile.Kind}] read={readState}, send={sendState}, auth={authMode}, readiness={readinessState}{warningSuffix}{errorSuffix}.";
    }

    private static bool MatchesProfileFilter(MailProfile profile, MailProfileOverviewQuery? query) {
        if (query == null) {
            return true;
        }

        if (query.Kind.HasValue && profile.Kind != query.Kind.Value) {
            return false;
        }

        if (query.DefaultOnly && !profile.IsDefault) {
            return false;
        }

        return true;
    }

    private static bool MatchesOverviewFilter(MailProfileOverview overview, MailProfileOverviewQuery? query) {
        if (query == null) {
            return true;
        }

        if (query.ReadyOnly && !overview.IsReady) {
            return false;
        }

        if (query.CanReadOnly && !overview.SupportsRead) {
            return false;
        }

        if (query.CanSendOnly && !overview.SupportsSend) {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<MailProfileOverview> SortOverviews(
        IReadOnlyList<MailProfileOverview> overviews,
        MailProfileOverviewQuery? query) {
        if (overviews.Count <= 1) {
            return overviews;
        }

        var sortBy = query?.SortBy ?? MailProfileOverviewSortBy.Id;
        var descending = query?.Descending == true;

        IOrderedEnumerable<MailProfileOverview> ordered = sortBy switch {
            MailProfileOverviewSortBy.Kind => overviews
                .OrderBy(overview => overview.Profile.Kind)
                .ThenBy(overview => overview.Profile.Id, StringComparer.OrdinalIgnoreCase),
            MailProfileOverviewSortBy.Readiness => overviews
                .OrderBy(overview => overview.IsReady)
                .ThenByDescending(overview => overview.ErrorCount)
                .ThenByDescending(overview => overview.WarningCount)
                .ThenBy(overview => overview.Profile.Id, StringComparer.OrdinalIgnoreCase),
            _ => overviews.OrderBy(overview => overview.Profile.Id, StringComparer.OrdinalIgnoreCase)
        };

        if (descending) {
            ordered = sortBy switch {
                MailProfileOverviewSortBy.Kind => overviews
                    .OrderByDescending(overview => overview.Profile.Kind)
                    .ThenByDescending(overview => overview.Profile.Id, StringComparer.OrdinalIgnoreCase),
                MailProfileOverviewSortBy.Readiness => overviews
                    .OrderByDescending(overview => overview.IsReady)
                    .ThenBy(overview => overview.ErrorCount)
                    .ThenBy(overview => overview.WarningCount)
                    .ThenBy(overview => overview.Profile.Id, StringComparer.OrdinalIgnoreCase),
                _ => overviews.OrderByDescending(overview => overview.Profile.Id, StringComparer.OrdinalIgnoreCase)
            };
        }

        return ordered.ToArray();
    }

    private static MailProfileOverviewCompact ToCompact(MailProfileOverview overview) => new() {
        Id = overview.Profile.Id,
        DisplayName = overview.Profile.DisplayName,
        Kind = overview.Profile.Kind,
        IsDefault = overview.Profile.IsDefault,
        SupportsRead = overview.SupportsRead,
        SupportsSend = overview.SupportsSend,
        AuthMode = overview.AuthStatus?.Mode,
        IsReady = overview.IsReady,
        ErrorCount = overview.ErrorCount,
        WarningCount = overview.WarningCount,
        Summary = overview.Summary
    };
}