namespace Mailozaurr;

/// <summary>
/// Builds a composed <see cref="MailApplication" /> with reusable default services.
/// </summary>
public sealed class MailApplicationBuilder {
    private readonly List<IMailReadHandler> _readHandlers = new();
    private readonly List<IMailMessageActionHandler> _messageActionHandlers = new();
    private readonly List<IMailSendHandler> _sendHandlers = new();
    private MailApplicationOptions _options = new();
    private IMailProfileStore? _profileStore;
    private IMailSecretStore? _secretStore;
    private IMailDraftStore? _draftStore;
    private IMailMessageActionPlanBatchStore? _messageActionPlanBatchStore;
    private IMailProfileService? _profileService;
    private IMailProfileOverviewService? _profileOverviewService;
    private IMailProfileConnectionService? _profileConnectionService;
    private IMailProfileSecretService? _profileSecretService;
    private IMailProfileSecretMaintenanceService? _profileSecretMaintenanceService;
    private IMailProfileBootstrapService? _profileBootstrapService;
    private IMailProfileAuthService? _profileAuthService;
    private IMailFolderAliasService? _folderAliasService;
    private IMailDraftService? _draftService;
    private IMailDraftExchangeService? _draftExchangeService;
    private IMailReadService? _readService;
    private IMailEmlExportService? _emlExportService;
    private IMailChangeFeedService? _changeFeedService;
    private IMailMessageActionPreviewService? _messageActionPreviewService;
    private IMailMessageActionPlanService? _messageActionPlanService;
    private IMailMessageActionPlanExchangeService? _messageActionPlanExchangeService;
    private IMailMessageActionPlanRegistryService? _messageActionPlanRegistryService;
    private IMailMessageActionBatchService? _messageActionBatchService;
    private IMailMessageActionService? _messageActionService;
    private IMailSendService? _sendService;
    private IMailQueueService? _queueService;
    private IDraftMimeMessageFactory? _draftMimeMessageFactory;
    private IPendingMessageRepository? _pendingMessageRepository;
    private IPendingMessageDeadLetterRepository? _pendingMessageDeadLetterRepository;
    private IImapSessionFactory? _imapSessionFactory;
    private IPop3SessionFactory? _pop3SessionFactory;
    private IGraphSessionFactory? _graphSessionFactory;
    private IGmailSessionFactory? _gmailSessionFactory;
    private ISmtpSessionFactory? _smtpSessionFactory;

    /// <summary>
    /// Creates a new builder with default options.
    /// </summary>
    public MailApplicationBuilder() {
    }

    /// <summary>
    /// Creates a new builder with the provided options.
    /// </summary>
    public MailApplicationBuilder(MailApplicationOptions options) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Replaces the current options.</summary>
    public MailApplicationBuilder Configure(MailApplicationOptions options) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>Uses an explicit profile store.</summary>
    public MailApplicationBuilder UseProfileStore(IMailProfileStore profileStore) {
        _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
        return this;
    }

    /// <summary>Uses an explicit secret store.</summary>
    public MailApplicationBuilder UseSecretStore(IMailSecretStore secretStore) {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        return this;
    }

    /// <summary>Uses an explicit draft store.</summary>
    public MailApplicationBuilder UseDraftStore(IMailDraftStore draftStore) {
        _draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        return this;
    }

    /// <summary>Uses an explicit message-action plan batch store.</summary>
    public MailApplicationBuilder UseMessageActionPlanBatchStore(IMailMessageActionPlanBatchStore messageActionPlanBatchStore) {
        _messageActionPlanBatchStore = messageActionPlanBatchStore ?? throw new ArgumentNullException(nameof(messageActionPlanBatchStore));
        return this;
    }

    /// <summary>Uses an explicit profile service.</summary>
    public MailApplicationBuilder UseProfileService(IMailProfileService profileService) {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        return this;
    }

    /// <summary>Uses an explicit profile overview service.</summary>
    public MailApplicationBuilder UseProfileOverviewService(IMailProfileOverviewService profileOverviewService) {
        _profileOverviewService = profileOverviewService ?? throw new ArgumentNullException(nameof(profileOverviewService));
        return this;
    }

    /// <summary>Uses an explicit profile connection-test service.</summary>
    public MailApplicationBuilder UseProfileConnectionService(IMailProfileConnectionService profileConnectionService) {
        _profileConnectionService = profileConnectionService ?? throw new ArgumentNullException(nameof(profileConnectionService));
        return this;
    }

    /// <summary>Uses an explicit profile secret service.</summary>
    public MailApplicationBuilder UseProfileSecretService(IMailProfileSecretService profileSecretService) {
        _profileSecretService = profileSecretService ?? throw new ArgumentNullException(nameof(profileSecretService));
        return this;
    }

    /// <summary>Uses an explicit orphaned profile-secret maintenance service.</summary>
    public MailApplicationBuilder UseProfileSecretMaintenanceService(
        IMailProfileSecretMaintenanceService profileSecretMaintenanceService) {
        _profileSecretMaintenanceService = profileSecretMaintenanceService ??
            throw new ArgumentNullException(nameof(profileSecretMaintenanceService));
        return this;
    }

    /// <summary>Uses an explicit profile bootstrap service.</summary>
    public MailApplicationBuilder UseProfileBootstrapService(IMailProfileBootstrapService profileBootstrapService) {
        _profileBootstrapService = profileBootstrapService ?? throw new ArgumentNullException(nameof(profileBootstrapService));
        return this;
    }

    /// <summary>Uses an explicit profile authentication service.</summary>
    public MailApplicationBuilder UseProfileAuthService(IMailProfileAuthService profileAuthService) {
        _profileAuthService = profileAuthService ?? throw new ArgumentNullException(nameof(profileAuthService));
        return this;
    }

    /// <summary>Uses an explicit folder alias discovery service.</summary>
    public MailApplicationBuilder UseFolderAliasService(IMailFolderAliasService folderAliasService) {
        _folderAliasService = folderAliasService ?? throw new ArgumentNullException(nameof(folderAliasService));
        return this;
    }

    /// <summary>Uses an explicit draft service.</summary>
    public MailApplicationBuilder UseDraftService(IMailDraftService draftService) {
        _draftService = draftService ?? throw new ArgumentNullException(nameof(draftService));
        return this;
    }

    /// <summary>Uses an explicit draft exchange service.</summary>
    public MailApplicationBuilder UseDraftExchangeService(IMailDraftExchangeService draftExchangeService) {
        _draftExchangeService = draftExchangeService ?? throw new ArgumentNullException(nameof(draftExchangeService));
        return this;
    }

    /// <summary>Uses an explicit read service.</summary>
    public MailApplicationBuilder UseReadService(IMailReadService readService) {
        _readService = readService ?? throw new ArgumentNullException(nameof(readService));
        return this;
    }

    /// <summary>Uses an explicit provider-neutral EML export service.</summary>
    public MailApplicationBuilder UseEmlExportService(IMailEmlExportService emlExportService) {
        _emlExportService = emlExportService ?? throw new ArgumentNullException(nameof(emlExportService));
        return this;
    }

    /// <summary>Uses an explicit normalized mailbox change-feed service.</summary>
    public MailApplicationBuilder UseChangeFeedService(IMailChangeFeedService changeFeedService) {
        _changeFeedService = changeFeedService ?? throw new ArgumentNullException(nameof(changeFeedService));
        return this;
    }

    /// <summary>Uses an explicit message-action preview service.</summary>
    public MailApplicationBuilder UseMessageActionPreviewService(IMailMessageActionPreviewService messageActionPreviewService) {
        _messageActionPreviewService = messageActionPreviewService ?? throw new ArgumentNullException(nameof(messageActionPreviewService));
        return this;
    }

    /// <summary>Uses an explicit message-action planning service.</summary>
    public MailApplicationBuilder UseMessageActionPlanService(IMailMessageActionPlanService messageActionPlanService) {
        _messageActionPlanService = messageActionPlanService ?? throw new ArgumentNullException(nameof(messageActionPlanService));
        return this;
    }

    /// <summary>Uses an explicit message-action plan exchange service.</summary>
    public MailApplicationBuilder UseMessageActionPlanExchangeService(IMailMessageActionPlanExchangeService messageActionPlanExchangeService) {
        _messageActionPlanExchangeService = messageActionPlanExchangeService ?? throw new ArgumentNullException(nameof(messageActionPlanExchangeService));
        return this;
    }

    /// <summary>Uses an explicit message-action plan registry service.</summary>
    public MailApplicationBuilder UseMessageActionPlanRegistryService(IMailMessageActionPlanRegistryService messageActionPlanRegistryService) {
        _messageActionPlanRegistryService = messageActionPlanRegistryService ?? throw new ArgumentNullException(nameof(messageActionPlanRegistryService));
        return this;
    }

    /// <summary>Uses an explicit message-action batch execution service.</summary>
    public MailApplicationBuilder UseMessageActionBatchService(IMailMessageActionBatchService messageActionBatchService) {
        _messageActionBatchService = messageActionBatchService ?? throw new ArgumentNullException(nameof(messageActionBatchService));
        return this;
    }

    /// <summary>Uses an explicit message-action service.</summary>
    public MailApplicationBuilder UseMessageActionService(IMailMessageActionService messageActionService) {
        _messageActionService = messageActionService ?? throw new ArgumentNullException(nameof(messageActionService));
        return this;
    }

    /// <summary>Uses an explicit send service.</summary>
    public MailApplicationBuilder UseSendService(IMailSendService sendService) {
        _sendService = sendService ?? throw new ArgumentNullException(nameof(sendService));
        return this;
    }

    /// <summary>Uses an explicit queue service.</summary>
    public MailApplicationBuilder UseQueueService(IMailQueueService queueService) {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        return this;
    }

    /// <summary>Uses an explicit draft MIME message factory.</summary>
    public MailApplicationBuilder UseDraftMimeMessageFactory(IDraftMimeMessageFactory draftMimeMessageFactory) {
        _draftMimeMessageFactory = draftMimeMessageFactory ?? throw new ArgumentNullException(nameof(draftMimeMessageFactory));
        return this;
    }

    /// <summary>Uses an explicit pending-message repository.</summary>
    public MailApplicationBuilder UsePendingMessageRepository(IPendingMessageRepository pendingMessageRepository) {
        _pendingMessageRepository = pendingMessageRepository ?? throw new ArgumentNullException(nameof(pendingMessageRepository));
        return this;
    }

    /// <summary>Uses an explicit pending-message dead-letter repository.</summary>
    public MailApplicationBuilder UsePendingMessageDeadLetterRepository(IPendingMessageDeadLetterRepository pendingMessageDeadLetterRepository) {
        _pendingMessageDeadLetterRepository = pendingMessageDeadLetterRepository ?? throw new ArgumentNullException(nameof(pendingMessageDeadLetterRepository));
        return this;
    }

    /// <summary>Uses an explicit IMAP session factory.</summary>
    public MailApplicationBuilder UseImapSessionFactory(IImapSessionFactory imapSessionFactory) {
        _imapSessionFactory = imapSessionFactory ?? throw new ArgumentNullException(nameof(imapSessionFactory));
        return this;
    }

    /// <summary>Uses an explicit POP3 session factory.</summary>
    public MailApplicationBuilder UsePop3SessionFactory(IPop3SessionFactory pop3SessionFactory) {
        _pop3SessionFactory = pop3SessionFactory ?? throw new ArgumentNullException(nameof(pop3SessionFactory));
        return this;
    }

    /// <summary>Uses an explicit Graph session factory.</summary>
    public MailApplicationBuilder UseGraphSessionFactory(IGraphSessionFactory graphSessionFactory) {
        _graphSessionFactory = graphSessionFactory ?? throw new ArgumentNullException(nameof(graphSessionFactory));
        return this;
    }

    /// <summary>Uses an explicit Gmail session factory.</summary>
    public MailApplicationBuilder UseGmailSessionFactory(IGmailSessionFactory gmailSessionFactory) {
        _gmailSessionFactory = gmailSessionFactory ?? throw new ArgumentNullException(nameof(gmailSessionFactory));
        return this;
    }

    /// <summary>Uses an explicit SMTP session factory.</summary>
    public MailApplicationBuilder UseSmtpSessionFactory(ISmtpSessionFactory smtpSessionFactory) {
        _smtpSessionFactory = smtpSessionFactory ?? throw new ArgumentNullException(nameof(smtpSessionFactory));
        return this;
    }

    /// <summary>Adds a read handler.</summary>
    public MailApplicationBuilder AddReadHandler(IMailReadHandler handler) {
        _readHandlers.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    /// <summary>Adds a message-action handler.</summary>
    public MailApplicationBuilder AddMessageActionHandler(IMailMessageActionHandler handler) {
        _messageActionHandlers.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    /// <summary>Adds a send handler.</summary>
    public MailApplicationBuilder AddSendHandler(IMailSendHandler handler) {
        _sendHandlers.Add(handler ?? throw new ArgumentNullException(nameof(handler)));
        return this;
    }

    /// <summary>
    /// Builds the composed application.
    /// </summary>
    public MailApplication Build() {
        var profileStore = _profileStore ?? new FileMailProfileStore(_options.ProfileStore);
        var secretStore = _secretStore ?? new FileMailSecretStore(_options.SecretStore);
        var draftStore = _draftStore ?? new FileMailDraftStore(_options.DraftStore);
        var messageActionPlanBatchStore = _messageActionPlanBatchStore ?? new FileMailMessageActionPlanBatchStore(_options.ActionPlanBatchStore);
        var imapSessionFactory = _imapSessionFactory ?? new ImapSessionFactory(secretStore);
        var pop3SessionFactory = _pop3SessionFactory ?? new Pop3SessionFactory(secretStore);
        var graphSessionFactory = _graphSessionFactory ?? new GraphSessionFactory(secretStore);
        var gmailSessionFactory = _gmailSessionFactory ?? new GmailSessionFactory(secretStore);
        var smtpSessionFactory = _smtpSessionFactory ?? new SmtpSessionFactory(secretStore);
        var profileSecretService = _profileSecretService ?? new MailProfileSecretService(profileStore, secretStore);
        var profileSecretMaintenanceService = _profileSecretMaintenanceService ??
            new MailProfileSecretMaintenanceService(profileStore, secretStore);
        var draftMimeMessageFactory = _draftMimeMessageFactory ?? new DraftMimeMessageFactory();
        var pendingMessageRepository = _pendingMessageRepository ?? new FilePendingMessageRepository(_options.PendingMessageStore);

        var readHandlers = new List<IMailReadHandler>(_readHandlers);
        if (_options.EnableImapReadHandler && !readHandlers.Any(handler => handler.Kind == MailProfileKind.Imap)) {
            readHandlers.Add(new ImapMailReadHandler(imapSessionFactory));
        }
        if (_options.EnablePop3ReadHandler && !readHandlers.Any(handler => handler.Kind == MailProfileKind.Pop3)) {
            readHandlers.Add(new Pop3MailReadHandler(pop3SessionFactory));
        }
        if (_options.EnableGraphReadHandler && !readHandlers.Any(handler => handler.Kind == MailProfileKind.Graph)) {
            readHandlers.Add(new GraphMailReadHandler(graphSessionFactory));
        }
        if (_options.EnableGmailReadHandler && !readHandlers.Any(handler => handler.Kind == MailProfileKind.Gmail)) {
            readHandlers.Add(new GmailMailReadHandler(gmailSessionFactory));
        }

        var messageActionHandlers = new List<IMailMessageActionHandler>(_messageActionHandlers);
        if (!messageActionHandlers.Any(handler => handler.Kind == MailProfileKind.Imap)) {
            messageActionHandlers.Add(new ImapMailMessageActionHandler(imapSessionFactory));
        }
        if (!messageActionHandlers.Any(handler => handler.Kind == MailProfileKind.Graph)) {
            messageActionHandlers.Add(new GraphMailMessageActionHandler(graphSessionFactory));
        }
        if (!messageActionHandlers.Any(handler => handler.Kind == MailProfileKind.Gmail)) {
            messageActionHandlers.Add(new GmailMailMessageActionHandler(gmailSessionFactory));
        }

        var sendHandlers = new List<IMailSendHandler>(_sendHandlers);
        if (_options.EnableGraphSendHandler && !sendHandlers.Any(handler => handler.Kind == MailProfileKind.Graph)) {
            sendHandlers.Add(new GraphMailSendHandler(graphSessionFactory, draftMimeMessageFactory, pendingMessageRepository));
        }
        if (_options.EnableGmailSendHandler && !sendHandlers.Any(handler => handler.Kind == MailProfileKind.Gmail)) {
            sendHandlers.Add(new GmailMailSendHandler(gmailSessionFactory, draftMimeMessageFactory, pendingMessageRepository));
        }
        if (_options.EnableSmtpSendHandler && !sendHandlers.Any(handler => handler.Kind == MailProfileKind.Smtp)) {
            sendHandlers.Add(new SmtpMailSendHandler(smtpSessionFactory, draftMimeMessageFactory, pendingMessageRepository));
        }
        if (_options.EnableSendGridSendHandler && !sendHandlers.Any(handler => handler.Kind == MailProfileKind.SendGrid)) {
            sendHandlers.Add(new SendGridMailSendHandler(secretStore, pendingMessageRepository));
        }
        if (_options.EnableMailgunSendHandler && !sendHandlers.Any(handler => handler.Kind == MailProfileKind.Mailgun)) {
            sendHandlers.Add(new MailgunMailSendHandler(secretStore, pendingMessageRepository));
        }
        if (_options.EnableSesSendHandler && !sendHandlers.Any(handler => handler.Kind == MailProfileKind.Ses)) {
            sendHandlers.Add(new SesMailSendHandler(secretStore, pendingMessageRepository));
        }

        var availableCapabilities = MailCapabilityCatalog.ForRegisteredHandlers(
            readHandlers,
            messageActionHandlers,
            sendHandlers,
            hasReadServiceOverride: _readService != null,
            hasMessageActionServiceOverride: _messageActionService != null,
            hasSendServiceOverride: _sendService != null,
            hasChangeFeedService: true);
        var profileService = _profileService ?? new MailProfileService(
            profileStore, secretStore, availableCapabilities);
        var profileBootstrapService = _profileBootstrapService ?? new MailProfileBootstrapService(profileService, profileSecretService, secretStore);
        var profileAuthService = _profileAuthService ?? new MailProfileAuthService(profileService, profileSecretService, secretStore);
        var profileOverviewService = _profileOverviewService ?? new MailProfileOverviewService(profileService, profileAuthService);
        var profileConnectionService = _profileConnectionService ?? MailProfileConnectionService.CreateWithPop3(
            profileStore,
            pop3SessionFactory,
            imapSessionFactory,
            graphSessionFactory,
            gmailSessionFactory,
            smtpSessionFactory);
        var draftService = _draftService ?? new MailDraftService(draftStore, profileStore);
        var draftExchangeService = _draftExchangeService ?? new JsonMailDraftExchangeService();

        var readService = _readService ?? new RoutedMailReadService(profileStore, readHandlers);
        var rawMessageSources = new IRawMailMessageSource[] {
            new ImapRawMailMessageSource(imapSessionFactory),
            new Pop3RawMailMessageSource(pop3SessionFactory),
            new GraphRawMailMessageSource(graphSessionFactory),
            new GmailRawMailMessageSource(gmailSessionFactory)
        };
        var emlExportService = _emlExportService ?? new MailEmlExportService(profileStore, rawMessageSources);
        var changeFeedService = _changeFeedService ?? new MailChangeFeedService(
            profileStore,
            imapSessionFactory,
            graphSessionFactory,
            gmailSessionFactory);
        var folderAliasService = _folderAliasService ?? new MailFolderAliasService(
            profileStore, readService, availableCapabilities);
        var messageActionPreviewService = _messageActionPreviewService ?? new MailMessageActionPreviewService(
            profileStore, folderAliasService, availableCapabilities);
        var messageActionService = _messageActionService ?? new RoutedMailMessageActionService(profileStore, messageActionHandlers, folderAliasService);
        var messageActionPlanService = _messageActionPlanService ?? new MailMessageActionPlanService(messageActionPreviewService, messageActionService);
        var messageActionPlanExchangeService = _messageActionPlanExchangeService ?? new JsonMailMessageActionPlanExchangeService();
        var messageActionBatchService = _messageActionBatchService ?? new MailMessageActionBatchService(messageActionPlanService);
        var messageActionPlanRegistryService = _messageActionPlanRegistryService ?? new MailMessageActionPlanRegistryService(messageActionPlanBatchStore, messageActionPlanExchangeService, messageActionPreviewService, messageActionPlanService, messageActionBatchService, profileStore);
        var sendService = _sendService ?? new RoutedMailSendService(profileStore, sendHandlers);
        var queueService = _queueService ?? new PendingMailQueueService(pendingMessageRepository, ResolvePendingMessageDeadLetterRepository());

        return new MailApplication(
            profileStore,
            secretStore,
            draftStore,
            profileService,
            profileOverviewService,
            profileConnectionService,
            profileSecretService,
            profileSecretMaintenanceService,
            profileBootstrapService,
            profileAuthService,
            folderAliasService,
            draftService,
            draftExchangeService,
            readService,
            emlExportService,
            changeFeedService,
            messageActionPreviewService,
            messageActionPlanService,
            messageActionPlanExchangeService,
            messageActionPlanRegistryService,
            messageActionBatchService,
            messageActionService,
            sendService,
            queueService,
            readHandlers.AsReadOnly(),
            messageActionHandlers.AsReadOnly(),
            sendHandlers.AsReadOnly());
    }

    private IPendingMessageDeadLetterRepository ResolvePendingMessageDeadLetterRepository() {
        if (_pendingMessageDeadLetterRepository != null) {
            return _pendingMessageDeadLetterRepository;
        }

        if (_pendingMessageRepository != null) {
            throw new InvalidOperationException("UsePendingMessageDeadLetterRepository must be configured when UsePendingMessageRepository overrides the default pending-message repository.");
        }

        return new FilePendingMessageDeadLetterRepository(_options.PendingMessageStore);
    }
}
