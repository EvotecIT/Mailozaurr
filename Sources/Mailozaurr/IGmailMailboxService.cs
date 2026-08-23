namespace Mailozaurr;

/// <summary>Application service for Gmail filters, labels, and threads.</summary>
public interface IGmailMailboxService {
    /// <summary>Lists Gmail filters.</summary>
    Task<IReadOnlyList<GmailFilter>> ListFiltersAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Gets one Gmail filter.</summary>
    Task<GmailFilter> GetFilterAsync(string profileId, string filterId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Creates one Gmail filter.</summary>
    Task<GmailFilter> CreateFilterAsync(string profileId, GmailFilter filter, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Deletes one Gmail filter. Gmail filters are immutable; replace by delete and create.</summary>
    Task DeleteFilterAsync(string profileId, string filterId, string? mailboxId = null, CancellationToken cancellationToken = default);

    /// <summary>Lists Gmail labels.</summary>
    Task<IReadOnlyList<GmailLabel>> ListLabelsAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Gets one Gmail label.</summary>
    Task<GmailLabel> GetLabelAsync(string profileId, string labelId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Creates one Gmail user label.</summary>
    Task<GmailLabel> CreateLabelAsync(string profileId, GmailLabel label, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Updates one Gmail user label.</summary>
    Task<GmailLabel> UpdateLabelAsync(string profileId, string labelId, GmailLabel label, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Deletes one Gmail user label.</summary>
    Task DeleteLabelAsync(string profileId, string labelId, string? mailboxId = null, CancellationToken cancellationToken = default);

    /// <summary>Lists exactly one provider page of Gmail threads.</summary>
    Task<GmailApiClient.GmailThreadPage> ListThreadsAsync(string profileId, string? mailboxId = null, string? query = null, int pageSize = 100, string? pageToken = null, CancellationToken cancellationToken = default);
    /// <summary>Gets one Gmail thread with its messages.</summary>
    Task<GmailThread> GetThreadAsync(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Adds and removes labels on one Gmail thread.</summary>
    Task<GmailThread> ModifyThreadLabelsAsync(string profileId, string threadId, IReadOnlyCollection<string>? addLabelIds = null, IReadOnlyCollection<string>? removeLabelIds = null, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Moves one Gmail thread to trash.</summary>
    Task<GmailThread> TrashThreadAsync(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Permanently deletes one Gmail thread.</summary>
    Task DeleteThreadAsync(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default);
}
