namespace Mailozaurr.Application;

/// <summary>
/// Represents a composed Mailozaurr application service graph for adapters.
/// </summary>
public sealed class MailApplication {
    internal MailApplication(
        IMailProfileStore profileStore,
        IMailSecretStore secretStore,
        IMailDraftStore draftStore,
        IMailProfileService profiles,
        IMailProfileOverviewService profileOverview,
        IMailProfileConnectionService profileConnections,
        IMailProfileSecretService profileSecrets,
        IMailProfileBootstrapService profileBootstrap,
        IMailProfileAuthService profileAuth,
        IMailDraftService drafts,
        IMailDraftExchangeService draftExchange,
        IMailReadService read,
        IMailSendService send,
        IMailQueueService queue,
        IReadOnlyList<IMailReadHandler> readHandlers,
        IReadOnlyList<IMailSendHandler> sendHandlers) {
        ProfileStore = profileStore;
        SecretStore = secretStore;
        DraftStore = draftStore;
        Profiles = profiles;
        ProfileOverview = profileOverview;
        ProfileConnections = profileConnections;
        ProfileSecrets = profileSecrets;
        ProfileBootstrap = profileBootstrap;
        ProfileAuth = profileAuth;
        Drafts = drafts;
        DraftExchange = draftExchange;
        Read = read;
        Send = send;
        Queue = queue;
        ReadHandlers = readHandlers;
        SendHandlers = sendHandlers;
    }

    /// <summary>Profile persistence service.</summary>
    public IMailProfileStore ProfileStore { get; }

    /// <summary>Secret persistence service.</summary>
    public IMailSecretStore SecretStore { get; }

    /// <summary>Draft persistence service.</summary>
    public IMailDraftStore DraftStore { get; }

    /// <summary>Profile lifecycle service.</summary>
    public IMailProfileService Profiles { get; }

    /// <summary>Aggregated profile overview service.</summary>
    public IMailProfileOverviewService ProfileOverview { get; }

    /// <summary>Live profile connection-test service.</summary>
    public IMailProfileConnectionService ProfileConnections { get; }

    /// <summary>Profile secret lifecycle service.</summary>
    public IMailProfileSecretService ProfileSecrets { get; }

    /// <summary>Higher-level profile bootstrap workflows.</summary>
    public IMailProfileBootstrapService ProfileBootstrap { get; }

    /// <summary>Higher-level profile authentication workflows.</summary>
    public IMailProfileAuthService ProfileAuth { get; }

    /// <summary>Draft lifecycle service.</summary>
    public IMailDraftService Drafts { get; }

    /// <summary>Draft import/export service.</summary>
    public IMailDraftExchangeService DraftExchange { get; }

    /// <summary>Normalized read service.</summary>
    public IMailReadService Read { get; }

    /// <summary>Normalized send service.</summary>
    public IMailSendService Send { get; }

    /// <summary>Normalized outbound queue service.</summary>
    public IMailQueueService Queue { get; }

    /// <summary>Registered read handlers.</summary>
    public IReadOnlyList<IMailReadHandler> ReadHandlers { get; }

    /// <summary>Registered send handlers.</summary>
    public IReadOnlyList<IMailSendHandler> SendHandlers { get; }
}
