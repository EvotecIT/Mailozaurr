namespace Mailozaurr;

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
        IMailProfileSecretMaintenanceService profileSecretMaintenance,
        IMailProfileBootstrapService profileBootstrap,
        IMailProfileAuthService profileAuth,
        IMailFolderAliasService folderAliases,
        IMailDraftService drafts,
        IMailDraftExchangeService draftExchange,
        IMailReadService read,
        IMailEmlExportService emlExport,
        IMailChangeFeedService changeFeeds,
        IMailMessageActionPreviewService messageActionPreview,
        IMailMessageActionPlanService messageActionPlans,
        IMailMessageActionPlanExchangeService messageActionPlanExchange,
        IMailMessageActionPlanRegistryService messageActionPlanRegistry,
        IMailMessageActionBatchService messageActionBatch,
        IMailMessageActionService messageActions,
        IMailSendService send,
        IMailQueueService queue,
        IReadOnlyList<IMailReadHandler> readHandlers,
        IReadOnlyList<IMailMessageActionHandler> messageActionHandlers,
        IReadOnlyList<IMailSendHandler> sendHandlers) {
        ProfileStore = profileStore;
        SecretStore = secretStore;
        DraftStore = draftStore;
        Profiles = profiles;
        ProfileOverview = profileOverview;
        ProfileConnections = profileConnections;
        ProfileSecrets = profileSecrets;
        ProfileSecretMaintenance = profileSecretMaintenance;
        ProfileBootstrap = profileBootstrap;
        ProfileAuth = profileAuth;
        FolderAliases = folderAliases;
        Drafts = drafts;
        DraftExchange = draftExchange;
        Read = read;
        EmlExport = emlExport;
        ChangeFeeds = changeFeeds;
        MessageActionPreview = messageActionPreview;
        MessageActionPlans = messageActionPlans;
        MessageActionPlanExchange = messageActionPlanExchange;
        MessageActionPlanRegistry = messageActionPlanRegistry;
        MessageActionBatch = messageActionBatch;
        MessageActions = messageActions;
        Send = send;
        Queue = queue;
        ReadHandlers = readHandlers;
        MessageActionHandlers = messageActionHandlers;
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

    /// <summary>Orphaned profile-secret inspection and cleanup service.</summary>
    public IMailProfileSecretMaintenanceService ProfileSecretMaintenance { get; }

    /// <summary>Higher-level profile bootstrap workflows.</summary>
    public IMailProfileBootstrapService ProfileBootstrap { get; }

    /// <summary>Higher-level profile authentication workflows.</summary>
    public IMailProfileAuthService ProfileAuth { get; }

    /// <summary>Provider-neutral folder alias discovery service.</summary>
    public IMailFolderAliasService FolderAliases { get; }

    /// <summary>Draft lifecycle service.</summary>
    public IMailDraftService Drafts { get; }

    /// <summary>Draft import/export service.</summary>
    public IMailDraftExchangeService DraftExchange { get; }

    /// <summary>Normalized read service.</summary>
    public IMailReadService Read { get; }

    /// <summary>Provider-neutral, lossless EML export service.</summary>
    public IMailEmlExportService EmlExport { get; }

    /// <summary>Normalized durable and live mailbox change-feed service.</summary>
    public IMailChangeFeedService ChangeFeeds { get; }

    /// <summary>Normalized dry-run mailbox action preview service.</summary>
    public IMailMessageActionPreviewService MessageActionPreview { get; }

    /// <summary>Normalized mailbox action planning service.</summary>
    public IMailMessageActionPlanService MessageActionPlans { get; }

    /// <summary>Normalized mailbox action plan import/export service.</summary>
    public IMailMessageActionPlanExchangeService MessageActionPlanExchange { get; }

    /// <summary>Normalized persisted mailbox action plan batch registry service.</summary>
    public IMailMessageActionPlanRegistryService MessageActionPlanRegistry { get; }

    /// <summary>Normalized mailbox action batch execution service.</summary>
    public IMailMessageActionBatchService MessageActionBatch { get; }

    /// <summary>Normalized mailbox message action service.</summary>
    public IMailMessageActionService MessageActions { get; }

    /// <summary>Normalized send service.</summary>
    public IMailSendService Send { get; }

    /// <summary>Normalized outbound queue service.</summary>
    public IMailQueueService Queue { get; }

    /// <summary>Registered read handlers.</summary>
    public IReadOnlyList<IMailReadHandler> ReadHandlers { get; }

    /// <summary>Registered message-action handlers.</summary>
    public IReadOnlyList<IMailMessageActionHandler> MessageActionHandlers { get; }

    /// <summary>Registered send handlers.</summary>
    public IReadOnlyList<IMailSendHandler> SendHandlers { get; }
}
