namespace Mailozaurr;

/// <summary>Application service for Microsoft Graph rules, events, and conversation threads.</summary>
public interface IGraphMailboxService {
    /// <summary>Lists Inbox rules for a Graph profile.</summary>
    Task<IReadOnlyList<GraphInboxRule>> ListRulesAsync(string profileId, string? mailboxId = null, string? filter = null, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default);
    /// <summary>Gets one Inbox rule.</summary>
    Task<GraphInboxRule> GetRuleAsync(string profileId, string ruleId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Creates one Inbox rule.</summary>
    Task<GraphInboxRule> CreateRuleAsync(string profileId, GraphInboxRule rule, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Updates one Inbox rule.</summary>
    Task<GraphInboxRule> UpdateRuleAsync(string profileId, string ruleId, GraphInboxRule rule, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Deletes one Inbox rule.</summary>
    Task DeleteRuleAsync(string profileId, string ruleId, string? mailboxId = null, CancellationToken cancellationToken = default);

    /// <summary>Lists calendar events for a Graph profile.</summary>
    Task<IReadOnlyList<GraphEvent>> ListEventsAsync(string profileId, string? mailboxId = null, string? filter = null, string? select = GraphApiClient.DefaultEventSelect, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default);
    /// <summary>Gets one calendar event.</summary>
    Task<GraphEvent> GetEventAsync(string profileId, string eventId, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Creates one calendar event.</summary>
    Task<GraphEvent> CreateEventAsync(string profileId, GraphEvent graphEvent, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Updates one calendar event.</summary>
    Task<GraphEvent> UpdateEventAsync(string profileId, string eventId, GraphEvent graphEvent, string? mailboxId = null, CancellationToken cancellationToken = default);
    /// <summary>Deletes one calendar event.</summary>
    Task DeleteEventAsync(string profileId, string eventId, string? mailboxId = null, CancellationToken cancellationToken = default);

    /// <summary>Lists messages belonging to one Graph conversation.</summary>
    Task<IReadOnlyList<GraphMailMessage>> GetThreadAsync(string profileId, string conversationId, string? mailboxId = null, int top = 100, int maxPages = 25, CancellationToken cancellationToken = default);
}
