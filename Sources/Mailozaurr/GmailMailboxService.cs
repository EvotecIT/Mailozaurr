namespace Mailozaurr;

/// <summary>Default profile-backed Gmail mailbox application service.</summary>
public sealed class GmailMailboxService : IGmailMailboxService {
    private readonly IMailProfileStore _profileStore;
    private readonly IGmailSessionFactory _sessionFactory;

    /// <summary>Creates a Gmail mailbox service.</summary>
    public GmailMailboxService(IMailProfileStore profileStore, IGmailSessionFactory sessionFactory) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GmailFilter>> ListFiltersAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.ManageRules, (session, token) => session.Client.ListFiltersAsync(session.UserId, token), cancellationToken);
    /// <inheritdoc />
    public Task<GmailFilter> GetFilterAsync(string profileId, string filterId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.ManageRules, (session, token) => session.Client.GetFilterAsync(session.UserId, filterId, token), cancellationToken);
    /// <inheritdoc />
    public Task<GmailFilter> CreateFilterAsync(string profileId, GmailFilter filter, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.ManageRules, (session, token) => session.Client.CreateFilterAsync(session.UserId, filter, token), cancellationToken);
    /// <inheritdoc />
    public Task DeleteFilterAsync(string profileId, string filterId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.ManageRules, (session, token) => session.Client.DeleteFilterAsync(session.UserId, filterId, token), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GmailLabel>> ListLabelsAsync(string profileId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseLabels, async (session, token) => (IReadOnlyList<GmailLabel>)await session.Client.ListLabelsAsync(session.UserId, token).ConfigureAwait(false), cancellationToken);
    /// <inheritdoc />
    public Task<GmailLabel> GetLabelAsync(string profileId, string labelId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseLabels, (session, token) => session.Client.GetLabelAsync(session.UserId, labelId, token), cancellationToken);
    /// <inheritdoc />
    public Task<GmailLabel> CreateLabelAsync(string profileId, GmailLabel label, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseLabels, (session, token) => session.Client.CreateLabelAsync(session.UserId, label, token), cancellationToken);
    /// <inheritdoc />
    public Task<GmailLabel> UpdateLabelAsync(string profileId, string labelId, GmailLabel label, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseLabels, (session, token) => session.Client.UpdateLabelAsync(session.UserId, labelId, label, token), cancellationToken);
    /// <inheritdoc />
    public Task DeleteLabelAsync(string profileId, string labelId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseLabels, (session, token) => session.Client.DeleteLabelAsync(session.UserId, labelId, token), cancellationToken);

    /// <inheritdoc />
    public Task<GmailApiClient.GmailThreadPage> ListThreadsAsync(string profileId, string? mailboxId = null, string? query = null, int pageSize = 100, string? pageToken = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseThreads, (session, token) => session.Client.ListThreadsPageAsync(session.UserId, query, pageSize, pageToken, token), cancellationToken);
    /// <inheritdoc />
    public Task<GmailThread> GetThreadAsync(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseThreads, (session, token) => session.Client.GetThreadAsync(session.UserId, threadId, token), cancellationToken);
    /// <inheritdoc />
    public Task<GmailThread> ModifyThreadLabelsAsync(string profileId, string threadId, IReadOnlyCollection<string>? addLabelIds = null, IReadOnlyCollection<string>? removeLabelIds = null, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseThreads, (session, token) => session.Client.ModifyThreadLabelsAsync(session.UserId, threadId, addLabelIds, removeLabelIds, token), cancellationToken);
    /// <inheritdoc />
    public Task<GmailThread> TrashThreadAsync(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseThreads, (session, token) => session.Client.TrashThreadAsync(session.UserId, threadId, token), cancellationToken);
    /// <inheritdoc />
    public Task DeleteThreadAsync(string profileId, string threadId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, MailCapability.UseThreads, (session, token) => session.Client.DeleteThreadAsync(session.UserId, threadId, token), cancellationToken);

    private async Task<T> WithSessionAsync<T>(string profileId, string? mailboxId, MailCapability capability, Func<GmailSession, CancellationToken, Task<T>> action, CancellationToken cancellationToken) {
        var profile = await ProviderMailboxProfileResolver.GetAsync(_profileStore, profileId, MailProfileKind.Gmail, mailboxId, cancellationToken).ConfigureAwait(false);
        RequireCapability(profile, capability);
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await action(session, cancellationToken).ConfigureAwait(false);
    }

    private async Task WithSessionAsync(string profileId, string? mailboxId, MailCapability capability, Func<GmailSession, CancellationToken, Task> action, CancellationToken cancellationToken) {
        var profile = await ProviderMailboxProfileResolver.GetAsync(_profileStore, profileId, MailProfileKind.Gmail, mailboxId, cancellationToken).ConfigureAwait(false);
        RequireCapability(profile, capability);
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        await action(session, cancellationToken).ConfigureAwait(false);
    }

    private static void RequireCapability(MailProfile profile, MailCapability capability) {
        if (!profile.GetCapabilities().Supports(capability)) {
            throw new NotSupportedException($"Profile '{profile.Id}' does not allow '{capability}'.");
        }
    }
}
