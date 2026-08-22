namespace Mailozaurr;

/// <summary>Default profile-backed Graph mailbox application service.</summary>
public sealed class GraphMailboxService : IGraphMailboxService {
    private readonly IMailProfileStore _profileStore;
    private readonly IGraphSessionFactory _sessionFactory;

    /// <summary>Creates a Graph mailbox service.</summary>
    public GraphMailboxService(IMailProfileStore profileStore, IGraphSessionFactory sessionFactory) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GraphInboxRule>> ListRulesAsync(string profileId, string? mailboxId = null, string? filter = null, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.ListInboxRulesAsync(session.UserId, filter, top, maxPages, token), cancellationToken);

    /// <inheritdoc />
    public Task<GraphInboxRule> GetRuleAsync(string profileId, string ruleId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.GetInboxRuleAsync(ruleId, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task<GraphInboxRule> CreateRuleAsync(string profileId, GraphInboxRule rule, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.CreateInboxRuleAsync(rule, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task<GraphInboxRule> UpdateRuleAsync(string profileId, string ruleId, GraphInboxRule rule, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.UpdateInboxRuleAsync(ruleId, rule, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task DeleteRuleAsync(string profileId, string ruleId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.DeleteInboxRuleAsync(ruleId, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GraphEvent>> ListEventsAsync(string profileId, string? mailboxId = null, string? filter = null, string? select = GraphApiClient.DefaultEventSelect, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.ListEventsAsync(session.UserId, filter, select, top, maxPages, token), cancellationToken);

    /// <inheritdoc />
    public Task<GraphEvent> GetEventAsync(string profileId, string eventId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.GetEventAsync(eventId, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task<GraphEvent> CreateEventAsync(string profileId, GraphEvent graphEvent, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.CreateEventAsync(graphEvent, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task<GraphEvent> UpdateEventAsync(string profileId, string eventId, GraphEvent graphEvent, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.UpdateEventAsync(eventId, graphEvent, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task DeleteEventAsync(string profileId, string eventId, string? mailboxId = null, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.DeleteEventAsync(eventId, session.UserId, token), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GraphMailMessage>> GetThreadAsync(string profileId, string conversationId, string? mailboxId = null, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default) =>
        WithSessionAsync(profileId, mailboxId, (session, token) => session.Client.ListConversationMessagesAsync(conversationId, session.UserId, top, maxPages, cancellationToken: token), cancellationToken);

    private async Task<T> WithSessionAsync<T>(string profileId, string? mailboxId, Func<GraphSession, CancellationToken, Task<T>> action, CancellationToken cancellationToken) {
        var profile = await ProviderMailboxProfileResolver.GetAsync(_profileStore, profileId, MailProfileKind.Graph, mailboxId, cancellationToken).ConfigureAwait(false);
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await action(session, cancellationToken).ConfigureAwait(false);
    }

    private async Task WithSessionAsync(string profileId, string? mailboxId, Func<GraphSession, CancellationToken, Task> action, CancellationToken cancellationToken) {
        var profile = await ProviderMailboxProfileResolver.GetAsync(_profileStore, profileId, MailProfileKind.Graph, mailboxId, cancellationToken).ConfigureAwait(false);
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        await action(session, cancellationToken).ConfigureAwait(false);
    }
}
