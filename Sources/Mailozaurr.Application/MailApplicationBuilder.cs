namespace Mailozaurr.Application;

/// <summary>
/// Builds a composed <see cref="MailApplication" /> with reusable default services.
/// </summary>
public sealed class MailApplicationBuilder {
    private readonly List<IMailReadHandler> _readHandlers = new();
    private readonly List<IMailSendHandler> _sendHandlers = new();
    private MailApplicationOptions _options = new();
    private IMailProfileStore? _profileStore;
    private IMailSecretStore? _secretStore;
    private IMailDraftStore? _draftStore;
    private IMailProfileService? _profileService;
    private IMailProfileOverviewService? _profileOverviewService;
    private IMailProfileConnectionService? _profileConnectionService;
    private IMailProfileSecretService? _profileSecretService;
    private IMailProfileBootstrapService? _profileBootstrapService;
    private IMailProfileAuthService? _profileAuthService;
    private IMailDraftService? _draftService;
    private IMailDraftExchangeService? _draftExchangeService;
    private IMailReadService? _readService;
    private IMailSendService? _sendService;
    private IMailQueueService? _queueService;
    private IDraftMimeMessageFactory? _draftMimeMessageFactory;
    private IPendingMessageRepository? _pendingMessageRepository;
    private IImapSessionFactory? _imapSessionFactory;
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

    /// <summary>Uses an explicit IMAP session factory.</summary>
    public MailApplicationBuilder UseImapSessionFactory(IImapSessionFactory imapSessionFactory) {
        _imapSessionFactory = imapSessionFactory ?? throw new ArgumentNullException(nameof(imapSessionFactory));
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
        var profileService = _profileService ?? new MailProfileService(profileStore, secretStore);
        var imapSessionFactory = _imapSessionFactory ?? new ImapSessionFactory(secretStore);
        var graphSessionFactory = _graphSessionFactory ?? new GraphSessionFactory(secretStore);
        var gmailSessionFactory = _gmailSessionFactory ?? new GmailSessionFactory(secretStore);
        var smtpSessionFactory = _smtpSessionFactory ?? new SmtpSessionFactory(secretStore);
        var profileSecretService = _profileSecretService ?? new MailProfileSecretService(profileStore, secretStore);
        var profileBootstrapService = _profileBootstrapService ?? new MailProfileBootstrapService(profileService, profileSecretService, secretStore);
        var profileAuthService = _profileAuthService ?? new MailProfileAuthService(profileService, profileSecretService, secretStore);
        var profileOverviewService = _profileOverviewService ?? new MailProfileOverviewService(profileService, profileAuthService);
        var profileConnectionService = _profileConnectionService ?? new MailProfileConnectionService(profileStore, imapSessionFactory, graphSessionFactory, gmailSessionFactory, smtpSessionFactory);
        var draftService = _draftService ?? new MailDraftService(draftStore, profileStore);
        var draftExchangeService = _draftExchangeService ?? new JsonMailDraftExchangeService();
        var draftMimeMessageFactory = _draftMimeMessageFactory ?? new DraftMimeMessageFactory();
        var pendingMessageRepository = _pendingMessageRepository ?? new FilePendingMessageRepository(_options.PendingMessageStore);

        var readHandlers = new List<IMailReadHandler>(_readHandlers);
        if (_options.EnableImapReadHandler && !readHandlers.Any(handler => handler.Kind == MailProfileKind.Imap)) {
            readHandlers.Add(new ImapMailReadHandler(imapSessionFactory));
        }
        if (_options.EnableGraphReadHandler && !readHandlers.Any(handler => handler.Kind == MailProfileKind.Graph)) {
            readHandlers.Add(new GraphMailReadHandler(graphSessionFactory));
        }
        if (_options.EnableGmailReadHandler && !readHandlers.Any(handler => handler.Kind == MailProfileKind.Gmail)) {
            readHandlers.Add(new GmailMailReadHandler(gmailSessionFactory));
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

        var readService = _readService ?? new RoutedMailReadService(profileStore, readHandlers);
        var sendService = _sendService ?? new RoutedMailSendService(profileStore, sendHandlers);
        var queueService = _queueService ?? new PendingMailQueueService(pendingMessageRepository);

        return new MailApplication(
            profileStore,
            secretStore,
            draftStore,
            profileService,
            profileOverviewService,
            profileConnectionService,
            profileSecretService,
            profileBootstrapService,
            profileAuthService,
            draftService,
            draftExchangeService,
            readService,
            sendService,
            queueService,
            readHandlers.AsReadOnly(),
            sendHandlers.AsReadOnly());
    }
}
