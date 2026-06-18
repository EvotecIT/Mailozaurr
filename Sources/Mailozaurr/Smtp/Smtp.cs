using Mailozaurr.Definitions;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// High level wrapper around <see cref="ClientSmtp"/> that exposes convenient methods and retry logic.
/// </summary>
/// <remarks>
/// Provides connection pooling and supports authentication using
/// multiple mechanisms depending on server capabilities.
/// <para>Instances are not thread-safe for concurrent operations. Calls to
/// <see cref="Send"/> or <see cref="SendAsync(System.Threading.CancellationToken)"/>
/// are serialized so that only one send executes at a time per instance.</para>
/// </remarks>
public partial class Smtp {
    private static ClientSmtp CreateDefaultClient(ProtocolLogger? logger) => logger == null ? new ClientSmtp() : new ClientSmtp(logger);

    /// <summary>Factory used to create <see cref="ClientSmtp"/> instances.</summary>
    public static Func<ProtocolLogger?, ClientSmtp> ClientFactory { get; set; } = CreateDefaultClient;

    /// <summary>Restores the default SMTP client factory.</summary>
    public static void ResetClientFactory() => ClientFactory = CreateDefaultClient;
    /// <summary>Configuration used for protocol logging.</summary>
    public LoggingConfigurator? Logging;

    /// <summary>LogCollector for capturing logs from async operations.</summary>
    public LogCollector? LogCollector { get; set; }

    /// <summary>
    /// When set, sending is simulated and no network or repository side-effects are performed.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>Repository used to persist sent message metadata.</summary>
    public ISentMessageRepository? SentMessageRepository { get; set; }
    /// <summary>Repository used to persist pending messages for later retry.</summary>
    public IPendingMessageRepository? PendingMessageRepository { get; set; }

    private const string ProviderDataSecureSocketOptionsKey = "SecureSocketOptions";
    private const string ProviderDataUseSslKey = "UseSsl";
    private const string ProviderDataSkipCertificateValidationKey = "SkipCertificateValidation";
    private const string ProviderDataCheckCertificateRevocationKey = "CheckCertificateRevocation";
    private const string ProviderDataTimeoutKey = "TimeoutMilliseconds";

    private string? _pendingMessagesPath;
    /// <summary>Directory path for storing pending messages.</summary>
    public string? PendingMessagesPath {
        get => _pendingMessagesPath;
        set {
            if (string.IsNullOrWhiteSpace(value)) {
                _pendingMessagesPath = value;
                return;
            }
            if (!string.Equals(_pendingMessagesPath, value, StringComparison.OrdinalIgnoreCase)) {
                var options = new PendingMessageRepositoryOptions { DirectoryPath = value! };
                PendingMessageRepository = new FilePendingMessageRepository(options);
            }
            _pendingMessagesPath = value;
        }
    }

    /// <summary>Underlying SMTP client used to send messages.</summary>
    public ClientSmtp Client { get; private set; }

    private SecureSocketOptions _activeSecureSocketOptions = SecureSocketOptions.Auto;
    private bool _activeUseSsl;

    /// <summary>Credentials used during authentication.</summary>
    public NetworkCredential? Credential { get; private set; }

    /// <summary>
    /// Effective secure socket options used by the active or most recent connection attempt.
    /// </summary>
    public SecureSocketOptions ActiveSecureSocketOptions => _activeSecureSocketOptions;

    /// <summary>
    /// Optional identity hint used to isolate SMTP connection pooling by credentials.
    /// </summary>
    public string? ConnectionPoolIdentity { get; set; }

    /// <summary>
    /// Optional per-instance override controlling whether this SMTP session should
    /// use the shared connection pool.
    /// </summary>
    public bool? UseConnectionPool { get; set; }

    /// <summary>Subject of the message.</summary>
    public string Subject {
        get => Client.Subject;
        set => Client.Subject = value;
    }

    /// <summary>HTML body of the message.</summary>
    public string HtmlBody {
        get => Client.HtmlBody;
        set => Client.HtmlBody = value;
    }

    /// <summary>Plain text body of the message.</summary>
    public string TextBody {
        get => Client.TextBody;
        set => Client.TextBody = value;
    }

    /// <summary>Attachments to include with the message.</summary>
    public List<AttachmentDescriptor>? Attachments {
        get => Client.Attachments;
        set => Client.Attachments = value;
    }

    /// <summary>Inline attachments to embed in the message.</summary>
    public List<AttachmentDescriptor>? InlineAttachments {
        get => Client.InlineAttachments;
        set => Client.InlineAttachments = value;
    }

    /// <summary>Custom headers to add to the message.</summary>
    public IDictionary<string, string>? Headers {
        get => Client.Headers;
        set => Client.Headers = value;
    }

    /// <summary>The sender address.</summary>
    public object? From {
        get => Client.From;
        set => Client.From = value;
    }

    /// <summary>Primary recipients.</summary>
    public IEnumerable<object>? To {
        get => Client.To;
        set => Client.To = value;
    }

    /// <summary>Carbon copy recipients.</summary>
    public IEnumerable<object>? Cc {
        get => Client.Cc;
        set => Client.Cc = value;
    }

    /// <summary>Blind carbon copy recipients.</summary>
    public IEnumerable<object>? Bcc {
        get => Client.Bcc;
        set => Client.Bcc = value;
    }

    /// <summary>Reply-to address.</summary>
    public object? ReplyTo {
        get => Client.ReplyTo;
        set => Client.ReplyTo = value;
    }

    /// <summary>The underlying MIME message.</summary>
    public MimeMessage Message {
        get => Client.Message;
        set => Client.Message = value;
    }

    /// <summary>Priority of the message.</summary>
    public MessagePriority Priority {
        get => Client.Priority;
        set => Client.Priority = value;
    }

    /// <summary>Delivery notification options.</summary>
    public DeliveryNotification[]? DeliveryNotificationOption {
        get => Client.DeliveryNotificationOption;
        set {
            if (value != null) {
                Client.DeliveryNotificationOption = value;
            }
        }
    }

    /// <summary>Timeout for SMTP operations in milliseconds.</summary>
    public int Timeout {
        get => Client.Timeout;
        set => Client.Timeout = value;
    }

    /// <summary>Number of retry attempts on failure.</summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>Base delay in milliseconds between retries.</summary>
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>Exponential backoff multiplier for retries.</summary>
    public double RetryDelayBackoff { get; set; } = 1.0;

    /// <summary>Maximum delay in milliseconds between retries. 0 disables capping.</summary>
    public int MaxDelayMilliseconds { get; set; } = 0;

    /// <summary>Jitter window in milliseconds added to retry delay. 0 disables jitter.</summary>
    public int JitterMilliseconds { get; set; } = 0;

    /// <summary>
    /// When set to <see langword="true"/>, replaces local image references in
    /// <see cref="HtmlBody"/> with inline attachments.
    /// </summary>
    public bool AutoEmbedImages { get; set; } = false;

    /// <summary>
    /// When set to <see langword="true"/>, automatically builds the MIME message
    /// from the configured properties before sending when the message is empty.
    /// </summary>
    public bool AutoCreateMessage { get; set; } = false;

    /// <summary>
    /// When set to <see langword="true"/>, downloads remote images referenced in
    /// <see cref="HtmlBody"/> and embeds them as inline attachments.
    /// </summary>
    public bool AutoEmbedRemoteImages {
        get => Client.AutoEmbedRemoteImages;
        set => Client.AutoEmbedRemoteImages = value;
    }

    /// <summary>
    /// Forces retries even when the encountered error is not considered
    /// transient. By default retries occur only for transient failures.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    /// <summary>Webhook invoked after sending.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Validate the server certificate against revocation lists.</summary>
    public bool CheckCertificateRevocation {
        get => Client.CheckCertificateRevocation;
        set => Client.CheckCertificateRevocation = value;
    }

    private bool _skipCertificateValidation;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private string? _poolIdentity;

    private bool IsConnectionPoolingEnabled => UseConnectionPool ?? SmtpConnectionPool.PoolingEnabled;
    /// <summary>Skip server certificate validation.</summary>
    public bool SkipCertificateValidation {
        get => _skipCertificateValidation;
        set {
            _skipCertificateValidation = value;
            if (value) {
                Client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            } else {
                Client.ServerCertificateValidationCallback = null;
            }
        }
    }

    /// <summary>Domain name to use in the SMTP HELO.</summary>
    public string LocalDomain {
        get => Client.LocalDomain ?? string.Empty;
        set {
            if (value != "") Client.LocalDomain = value;
        }
    }

    /// <summary>Delivery status notification type to request.</summary>
    public DeliveryStatusNotificationType? DeliveryStatusNotificationType {
        get => Client.DeliveryStatusNotificationType;
        set {
            if (value != null) {
                Client.DeliveryStatusNotificationType = value.Value;
            }
        }
    }

    /// <summary>Custom certificate validation callback.</summary>
    public RemoteCertificateValidationCallback? ServerCertificateValidationCallback {
        get => Client.ServerCertificateValidationCallback;
        set => Client.ServerCertificateValidationCallback = value;
    }

    /// <summary>Port used to connect to the SMTP server.</summary>
    public int Port { get; private set; } = 25;

    /// <summary>SMTP server host name.</summary>
    public string Server { get; private set; } = String.Empty;

    /// <summary>Action to take when an error occurs.</summary>
    public ActionPreference? ErrorAction { get; set; }

    /// <summary>Comma separated list of all recipients.</summary>
    public string SentTo => Client.SentTo;
    /// <summary>Normalized address the message is sent from.</summary>
    public string SentFrom => Helpers.GetEmailAddress(From ?? string.Empty);

    /// <summary>Stopwatch measuring the time of operations.</summary>
    public readonly Stopwatch Stopwatch;

    /// <summary>
    /// Initializes a new instance of the <see cref="Smtp"/> class, with optional logging configuration.
    /// </summary>
    /// <param name="logging">The logging.</param>
    public Smtp(LoggingConfigurator? logging = null) {
        Stopwatch = Stopwatch.StartNew();
        Logging = logging;
        Client = ClientFactory(logging?.ProtocolLogger);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Smtp"/> class with logging configuration.
    /// </summary>
    /// <param name="logPath">The log path.</param>
    /// <param name="logConsole">if set to <c>true</c> [log console].</param>
    /// <param name="logObject">if set to <c>true</c> [log object].</param>
    /// <param name="logTimestamps">if set to <c>true</c> [log timestamps].</param>
    /// <param name="logSecrets">if set to <c>true</c> [log secrets].</param>
    /// <param name="logTimestampsFormat">The log timestamps format.</param>
    /// <param name="logServerPrefix">The log server prefix.</param>
    /// <param name="logClientPrefix">The log client prefix.</param>
    /// <param name="logOverwrite">if set to <c>true</c> [log overwrite].</param>
    public Smtp(string logPath, bool logConsole, bool logObject, bool logTimestamps, bool logSecrets,
        string? logTimestampsFormat = null, string? logServerPrefix = null, string? logClientPrefix = null,
        bool logOverwrite = false) {

        LogVerbose($"Send-EmailMessage - Logging configuration: Path: {logPath}, Console: {logConsole}, Object: {logObject}, Timestamps: {logTimestamps}, Secrets: {logSecrets}, TimestampsFormat: {logTimestampsFormat}, ServerPrefix: {logServerPrefix}, ClientPrefix: {logClientPrefix}, Overwrite: {logOverwrite}");
        Logging = new LoggingConfigurator();
        Logging.ConfigureLogging(logPath, logConsole, logObject, logTimestamps, logSecrets, logTimestampsFormat, logServerPrefix, logClientPrefix, logOverwrite);
        Client = ClientFactory(Logging.ProtocolLogger);
        Stopwatch = Stopwatch.StartNew();
    }


    /// <summary>
    /// Connects to the specified SMTP server and returns detailed information about the connection.
    /// </summary>
    /// <param name="server">SMTP server name.</param>
    /// <param name="port">Port number.</param>
    /// <param name="secureSocketOptions">Controls SSL/TLS usage.</param>
    /// <param name="useSsl">Compatibility flag overriding <paramref name="secureSocketOptions"/> when set.</param>
    public static SmtpConnectionInfo TestConnection(string server, int port, SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto, bool useSsl = false) {
        return TestConnection(server, port, secureSocketOptions, useSsl, null, "probe@example.com", "localhost");
    }

    /// <summary>
    /// Connects to the specified SMTP server and optionally tests recipient acceptance before DATA.
    /// </summary>
    /// <param name="server">SMTP server name.</param>
    /// <param name="port">Port number.</param>
    /// <param name="secureSocketOptions">Controls SSL/TLS usage.</param>
    /// <param name="useSsl">Compatibility flag overriding <paramref name="secureSocketOptions"/> when set.</param>
    /// <param name="recipient">Optional recipient address used in RCPT TO.</param>
    /// <param name="sender">Envelope sender address used in MAIL FROM when <paramref name="recipient"/> is supplied.</param>
    /// <param name="heloHost">EHLO/HELO name sent during the recipient probe.</param>
    public static SmtpConnectionInfo TestConnection(
        string server,
        int port,
        SecureSocketOptions secureSocketOptions,
        bool useSsl,
        string? recipient,
        string sender = "probe@example.com",
        string heloHost = "localhost") {
        return TestConnection(server, port, secureSocketOptions, useSsl, recipient, sender, heloHost, null);
    }

    /// <summary>
    /// Connects to the specified SMTP server and optionally tests recipient acceptance or sends a validation message.
    /// </summary>
    /// <param name="server">SMTP server name.</param>
    /// <param name="port">Port number.</param>
    /// <param name="secureSocketOptions">Controls SSL/TLS usage.</param>
    /// <param name="useSsl">Compatibility flag overriding <paramref name="secureSocketOptions"/> when set.</param>
    /// <param name="recipient">Optional recipient address used in RCPT TO.</param>
    /// <param name="sender">Envelope sender address used in MAIL FROM when <paramref name="recipient"/> is supplied.</param>
    /// <param name="heloHost">EHLO/HELO name sent during the recipient probe.</param>
    /// <param name="validationMessage">Optional validation message request.</param>
    public static SmtpConnectionInfo TestConnection(
        string server,
        int port,
        SecureSocketOptions secureSocketOptions,
        bool useSsl,
        string? recipient,
        string sender,
        string heloHost,
        SmtpValidationMessageRequest? validationMessage) {
        var logging = new LoggingConfigurator();
        logging.ConfigureLogging(null, false, true, false, false);

        var smtp = new Smtp(logging);
        _ = smtp.Connect(server, port, secureSocketOptions, useSsl);

        string? banner = null;
        string? software = null;

        if (logging.LogStream != null) {
            logging.LogStream.Position = 0;
            using var reader = new StreamReader(logging.LogStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line)) {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("<--", StringComparison.Ordinal)) {
                    trimmed = trimmed.Substring(3).Trim();
                }
                banner = trimmed;
                var parts = trimmed.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3) {
                    software = parts[2];
                }
            }
        }

        var persistent = false;
        try {
            smtp.Client.NoOp();
            persistent = smtp.Client.IsConnected;
        } catch {
            persistent = false;
        }

        var capabilities = smtp.Client.GetCapabilitiesSnapshot();
        smtp.Disconnect();
        smtp.Dispose();

        SmtpRecipientProbeInfo? recipientProbe = null;
        if (!string.IsNullOrWhiteSpace(recipient)) {
            recipientProbe = TestRecipient(server, port, recipient!, sender, heloHost, secureSocketOptions, useSsl);
        }

        SmtpValidationMessageInfo? validationMessageInfo = null;
        if (validationMessage != null) {
            validationMessageInfo = SendValidationMessage(server, port, validationMessage, secureSocketOptions, useSsl);
        }

        return new SmtpConnectionInfo(server, port, banner, software, capabilities, persistent, recipientProbe, validationMessageInfo);
    }

    /// <summary>
    /// Creates the MIME message using the current property values.
    /// </summary>
    public void CreateMessage(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        PrepareInlineAttachments();
        Client.CreateMessage(cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates the MIME message using the current property values.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task CreateMessageAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        PrepareInlineAttachments();
        await Client.CreateMessageAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the constructed message to the specified path.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    public void SaveMessage(string path) {
        if (!string.IsNullOrWhiteSpace(path)) {
            Client.SaveMessage(path);
        }
    }

    /// <summary>
    /// Asynchronously saves the constructed message to the specified path.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public Task SaveMessageAsync(string path, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            return Task.CompletedTask;
        }

        return Client.SaveMessageAsync(path, cancellationToken);
    }

    private void PrepareInlineAttachments() {
        if (!AutoEmbedImages) {
            return;
        }

        var (html, paths) = HtmlUtils.ExtractLocalImagePaths(HtmlBody);
        HtmlBody = html;
        if (paths.Count <= 0) {
            return;
        }

        InlineAttachments ??= new List<AttachmentDescriptor>();
        foreach (var path in paths) {
            if (InlineAttachments.Any(d => string.Equals(d.SourcePath, path, StringComparison.OrdinalIgnoreCase))) {
                continue;
            }

            InlineAttachments.Add(new FileAttachmentDescriptor(path));
        }
    }

    /// <summary>
    /// Connect to the SMTP server using the provided server and port.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="port"></param>
    /// <param name="secureSocketOptions">Options controlling SSL/TLS usage. If left
    /// as <see cref="SecureSocketOptions.Auto"/> and <paramref name="useSsl"/> is
    /// <c>true</c>, <see cref="SecureSocketOptions.StartTls"/> will be used.</param>
    /// <param name="useSsl">Compatibility switch. Overrides
    /// <paramref name="secureSocketOptions"/> only when set to <c>true</c> and the
    /// option is left as <see cref="SecureSocketOptions.Auto"/>.</param>
    /// <returns></returns>
    public SmtpResult Connect(string server, int port, SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto, bool useSsl = false) {
        var oldServer = Server;
        var oldPort = Port;
        var oldPoolIdentity = _poolIdentity ?? GetConnectionPoolIdentity();
        Server = server;
        Port = port;
        if (!SmtpValidation.TryValidateServer(server, port, out var validationError)) {
            string message = validationError ?? "Invalid SMTP server settings.";
            LogWarning($"Send-EmailMessage - {message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidOperationException(message);
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, server ?? string.Empty, port, Stopwatch.Elapsed, "", message);
        }
        var effectiveOptions = secureSocketOptions;
        if (useSsl && effectiveOptions == SecureSocketOptions.Auto) {
            // Maintain backwards compatibility with Send-MailMessage by
            // defaulting to StartTls when the UseSsl flag is supplied and
            // no explicit option was provided.
            effectiveOptions = SecureSocketOptions.StartTls;
        }
        _activeUseSsl = useSsl;
        _activeSecureSocketOptions = effectiveOptions;
        if (DryRun) {
            LogVerbose($"Send-EmailMessage - DryRun enabled, skipping connect to {server} on port {port} using SSL: {effectiveOptions}");
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, server, port, Stopwatch.Elapsed, "Connection skipped (WhatIf)");
        }
        if (Client.IsConnected) {
            if (IsConnectionPoolingEnabled) {
                SmtpConnectionPool.ReturnClient(oldServer, oldPort, Client, oldPoolIdentity, IsConnectionPoolingEnabled);
            } else {
                Client.Disconnect(true);
            }
            Client = ClientFactory(Logging?.ProtocolLogger);
        }

        var poolIdentity = GetConnectionPoolIdentity();
        var pooled = SmtpConnectionPool.TryRentClient(server, port, poolIdentity, IsConnectionPoolingEnabled);
        if (pooled != null) {
            Client = pooled;
        }
        try {
            if (!Client.IsConnected) {
                Client.Connect(server, port, effectiveOptions);
            }
            _poolIdentity = poolIdentity;
            LogVerbose($"Connected to {server} on {port} port using SSL: {effectiveOptions}");
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, server, port, Stopwatch.Elapsed, "");
        } catch (Exception ex) {
            LogWarning($"Send-EmailMessage - Error during connect: {ex.Message}");
            LogWarning($"Send-EmailMessage - Possible issue: Port? ({port} was used), Using SSL? ({effectiveOptions}, was used). You can also try 'SkipCertificateValidation' or 'SkipCertificateRevocation'.");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, server, port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously connect to the SMTP server using the provided server and port.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="port"></param>
    /// <param name="secureSocketOptions">Options controlling SSL/TLS usage. If left
    /// as <see cref="SecureSocketOptions.Auto"/> and <paramref name="useSsl"/> is
    /// <c>true</c>, <see cref="SecureSocketOptions.StartTls"/> will be used.</param>
    /// <param name="useSsl">Compatibility switch. Overrides
    /// <paramref name="secureSocketOptions"/> only when set to <c>true</c> and the
    /// option is left as <see cref="SecureSocketOptions.Auto"/>.</param>
    /// <returns></returns>
    public Task<SmtpResult> ConnectAsync(
        string server,
        int port,
        SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto,
        bool useSsl = false) {
        return ConnectAsync(server, port, secureSocketOptions, useSsl, CancellationToken.None);
    }

    /// <summary>
    /// Asynchronously connect to the SMTP server using the provided server and port.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="port"></param>
    /// <param name="secureSocketOptions">Options controlling SSL/TLS usage. If left
    /// as <see cref="SecureSocketOptions.Auto"/> and <paramref name="useSsl"/> is
    /// <c>true</c>, <see cref="SecureSocketOptions.StartTls"/> will be used.</param>
    /// <param name="useSsl">Compatibility switch. Overrides
    /// <paramref name="secureSocketOptions"/> only when set to <c>true</c> and the
    /// option is left as <see cref="SecureSocketOptions.Auto"/>.</param>
    /// <param name="cancellationToken">Cancellation token for the connect operation.</param>
    /// <returns></returns>
    public async Task<SmtpResult> ConnectAsync(
        string server,
        int port,
        SecureSocketOptions secureSocketOptions,
        bool useSsl,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var oldServer = Server;
        var oldPort = Port;
        var oldPoolIdentity = _poolIdentity ?? GetConnectionPoolIdentity();
        Server = server;
        Port = port;
        if (!SmtpValidation.TryValidateServer(server, port, out var validationError)) {
            string message = validationError ?? "Invalid SMTP server settings.";
            LogWarning($"Send-EmailMessage - {message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidOperationException(message);
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, server ?? string.Empty, port, Stopwatch.Elapsed, "", message);
        }
        var effectiveOptions = secureSocketOptions;
        if (useSsl && effectiveOptions == SecureSocketOptions.Auto) {
            // Maintain backwards compatibility with Send-MailMessage by
            // defaulting to StartTls when the UseSsl flag is supplied and
            // no explicit option was provided.
            effectiveOptions = SecureSocketOptions.StartTls;
        }
        _activeUseSsl = useSsl;
        _activeSecureSocketOptions = effectiveOptions;
        if (DryRun) {
            LogVerbose($"Send-EmailMessage - DryRun enabled, skipping connect to {server} on port {port} using SSL: {effectiveOptions}");
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, server, port, Stopwatch.Elapsed, "Connection skipped (WhatIf)");
        }
        if (Client.IsConnected) {
            if (IsConnectionPoolingEnabled) {
                SmtpConnectionPool.ReturnClient(oldServer, oldPort, Client, oldPoolIdentity, IsConnectionPoolingEnabled);
            } else {
                Client.Disconnect(true);
            }
            Client = ClientFactory(Logging?.ProtocolLogger);
        }

        var poolIdentity = GetConnectionPoolIdentity();
        var pooled = SmtpConnectionPool.TryRentClient(server, port, poolIdentity, IsConnectionPoolingEnabled);
        if (pooled != null) {
            Client = pooled;
        }
        try {
            if (!Client.IsConnected) {
                await Client.ConnectAsync(server, port, effectiveOptions, cancellationToken).ConfigureAwait(false);
            }
            _poolIdentity = poolIdentity;
            LogVerbose($"Connected to {server} on {port} port using SSL: {effectiveOptions}");
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, server, port, Stopwatch.Elapsed, "");
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LogWarning($"Send-EmailMessage - Error during connect: {ex.Message}");
            LogWarning($"Send-EmailMessage - Possible issue: Port? ({port} was used), Using SSL? ({effectiveOptions}, was used). You can also try 'SkipCertificateValidation' or 'SkipCertificateRevocation'.");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, server, port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// Connects and authenticates using the provided user name/secret in one step.
    /// </summary>
    /// <param name="server">SMTP server hostname.</param>
    /// <param name="port">SMTP server port.</param>
    /// <param name="userName">SMTP user name.</param>
    /// <param name="secret">SMTP password or OAuth token.</param>
    /// <param name="secureSocketOptions">TLS/SSL options.</param>
    /// <param name="useSsl">Compatibility SSL switch.</param>
    /// <param name="authMode">Authentication mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined connect/auth outcome.</returns>
    public async Task<SmtpConnectAuthenticateResult> ConnectAndAuthenticateAsync(
        string server,
        int port,
        string userName,
        string secret,
        SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto,
        bool useSsl = false,
        ProtocolAuthMode authMode = ProtocolAuthMode.Basic,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedUserName = userName?.Trim() ?? string.Empty;
        var previousConnectionPoolIdentity = ConnectionPoolIdentity;
        var shouldOverrideConnectionPoolIdentity =
            string.IsNullOrWhiteSpace(previousConnectionPoolIdentity) &&
            !string.IsNullOrWhiteSpace(normalizedUserName);

        if (shouldOverrideConnectionPoolIdentity) {
            ConnectionPoolIdentity = normalizedUserName;
        }

        SmtpResult connectResult;
        try {
            connectResult = await ConnectAsync(server, port, secureSocketOptions, useSsl, cancellationToken).ConfigureAwait(false);
        } finally {
            if (shouldOverrideConnectionPoolIdentity) {
                ConnectionPoolIdentity = previousConnectionPoolIdentity;
            }
        }

        if (!connectResult.Status) {
            return new SmtpConnectAuthenticateResult {
                IsSuccess = false,
                SecureSocketOptions = ActiveSecureSocketOptions,
                ErrorCode = "connect_failed",
                Error = connectResult.Error ?? "Connect failed.",
                IsTransient = SmtpValidation.TryValidateServer(server, port, out _)
            };
        }

        if (DryRun) {
            LogVerbose("Send-EmailMessage - DryRun enabled, skipping authentication.");
            Credential = new NetworkCredential(normalizedUserName, secret ?? string.Empty);
            return new SmtpConnectAuthenticateResult {
                IsSuccess = true,
                SecureSocketOptions = ActiveSecureSocketOptions
            };
        }

        try {
            await ProtocolAuth.AuthenticateSmtpAsync(Client, normalizedUserName, secret, authMode, cancellationToken).ConfigureAwait(false);
            Credential = new NetworkCredential(normalizedUserName, secret ?? string.Empty);
            return new SmtpConnectAuthenticateResult {
                IsSuccess = true,
                SecureSocketOptions = ActiveSecureSocketOptions
            };
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpConnectAuthenticateResult {
                IsSuccess = false,
                SecureSocketOptions = ActiveSecureSocketOptions,
                ErrorCode = "auth_failed",
                Error = ex.Message,
                IsTransient = false
            };
        }
    }

    /// <summary>
    /// Authenticate using the provided credentials.
    /// </summary>
    /// <param name="Credentials"></param>
    /// <param name="isOAuth"></param>
    /// <returns></returns>
    public SmtpResult Authenticate(ICredentials Credentials, bool isOAuth = false) {
        if (DryRun) {
            LogVerbose("Send-EmailMessage - DryRun enabled, skipping authentication.");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "Authentication skipped (WhatIf)");
        }
        if (Credentials is NetworkCredential networkCredential) {
            if (!SmtpValidation.TryValidateCredentials(networkCredential.UserName, networkCredential.Password, out var validationError)) {
                string message = validationError ?? "Invalid SMTP credentials.";
                LogWarning($"Send-EmailMessage - {message}");
                if (ErrorAction == ActionPreference.Stop) {
                    throw new InvalidOperationException(message);
                }
                return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", message);
            }
        }
        try {
            if (isOAuth) {
                var oauthCredential = Credentials as NetworkCredential;
                if (oauthCredential != null) {
                    Credential = oauthCredential;
                    var (userName, token) = Helpers.ConvertFromOAuth2Credential(oauthCredential);
                    var oauth2 = new SaslMechanismOAuth2(userName, token);
                    Client.Authenticate(oauth2);
                }
                LogVerbose($"Send-EmailMessage - Authenticated using oAuth");
            } else {
                Credential = Credentials as NetworkCredential;
                Client.Authenticate(Credentials);
            }
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LogWarning($"Send-EmailMessage - Error during authentication (oAuth): {ex.Message}");
            LogWarning($"Send-EmailMessage - Possible issue: OAuth? ({isOAuth} was used), ICredentials? ({Credentials}, was used).");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously authenticate using the provided credentials.
    /// </summary>
    /// <param name="Credentials"></param>
    /// <param name="isOAuth"></param>
    /// <returns></returns>
    public async Task<SmtpResult> AuthenticateAsync(ICredentials Credentials, bool isOAuth = false) {
        if (DryRun) {
            LogVerbose("Send-EmailMessage - DryRun enabled, skipping authentication.");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "Authentication skipped (WhatIf)");
        }
        try {
            if (isOAuth) {
                var networkCredential = Credentials as NetworkCredential;
                if (networkCredential != null) {
                    Credential = networkCredential;
                    var (userName, token) = Helpers.ConvertFromOAuth2Credential(networkCredential);
                    var oauth2 = new SaslMechanismOAuth2(userName, token);
                    await Client.AuthenticateAsync(oauth2);
                }
                LogVerbose($"Send-EmailMessage - Authenticated using oAuth");
            } else {
                Credential = Credentials as NetworkCredential;
                await Client.AuthenticateAsync(Credentials);
            }
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LogWarning($"Send-EmailMessage - Error during authentication (oAuth): {ex.Message}");
            LogWarning($"Send-EmailMessage - Possible issue: OAuth? ({isOAuth} was used), ICredentials? ({Credentials}, was used).");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// Authenticate using the default credentials of the current user (NTLM)
    /// </summary>
    /// <returns></returns>
    public SmtpResult AuthenticateDefaultCredentials() {
        if (DryRun) {
            LogVerbose("Send-EmailMessage - DryRun enabled, skipping authentication.");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "Authentication skipped (WhatIf)");
        }
        try {
            var mechanism = new SaslMechanismNtlmIntegrated();
            Client.Authenticate(mechanism);
            LogVerbose($"Send-EmailMessage - Authenticated using default credentials");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LogWarning($"Send-EmailMessage - Could not authenticate using default credentials. Error: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// Returns the plain text password, decrypting it when <paramref name="isSecureString"/> is true.
    /// </summary>
    /// <param name="password">Password value.</param>
    /// <param name="isSecureString">Indicates if the password is protected.</param>
    /// <returns>The plain text password.</returns>
    public string ConvertSecureStringToPlainString(string password, bool isSecureString) {
        if (isSecureString) {
            // Convert the encrypted string back to a SecureString
            SecureString securePassword = SecureStringHelper.Unprotect(password);

            // Convert the SecureString to a plain string
            IntPtr unmanagedString = IntPtr.Zero;
            try {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                return Marshal.PtrToStringUni(unmanagedString) ?? string.Empty;
            } finally {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
        return password;
    }

    /// <summary>
    /// Authenticate using the specified user name and password. After the
    /// authentication attempt, the plain text value is either overwritten or
    /// protected again to avoid leaving sensitive data in memory.
    /// </summary>
    /// <param name="username">The user name.</param>
    /// <param name="password">
    /// Password value. When <paramref name="isSecureString"/> is <c>true</c>, the
    /// string is re-secured using <see cref="SecureStringHelper.Protect"/> after
    /// authentication completes.
    /// </param>
    /// <param name="isSecureString">Indicates whether the password was
    /// previously protected.</param>
    /// <param name="mechanism">Authentication mechanism to use.</param>
    /// <returns>An <see cref="SmtpResult"/> representing the outcome.</returns>
    public SmtpResult Authenticate(string username, string password, bool isSecureString, AuthenticationMechanism mechanism = AuthenticationMechanism.Plain) {
        if (DryRun) {
            LogVerbose("Send-EmailMessage - DryRun enabled, skipping authentication.");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "Authentication skipped (WhatIf)");
        }
        password = ConvertSecureStringToPlainString(password, isSecureString);
        Credential = new NetworkCredential(username, password);
        try {
            if (!SmtpValidation.TryValidateCredentials(username, password, out var validationError)) {
                string message = validationError ?? "Invalid SMTP credentials.";
                LogWarning($"Send-EmailMessage - {message}");
                if (ErrorAction == ActionPreference.Stop) {
                    throw new InvalidOperationException(message);
                }
                return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", message);
            }
            switch (mechanism) {
                case AuthenticationMechanism.CramMd5:
                    Client.Authenticate(new SaslMechanismCramMd5(username, password));
                    break;
                case AuthenticationMechanism.Login:
                    Client.Authenticate(new SaslMechanismLogin(username, password));
                    break;
                default:
                    Client.Authenticate(new SaslMechanismPlain(username, password));
                    break;
            }
            LogVerbose($"Send-EmailMessage - Authenticated as {username}");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LogWarning($"Send-EmailMessage - Error during authentication: {ex.Message}");
            LogWarning($"Send-EmailMessage - Possible issue: Username? ({username} was used), Password?.");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
        } finally {
            if (isSecureString) {
                using var securePwd = SecureStringHelper.FromPlainTextString(password);
                password = SecureStringHelper.Protect(securePwd);
            } else {
                password = new string('\0', password.Length);
            }
        }
    }

    /// <summary>
    /// Disconnects from the SMTP server.
    /// </summary>
    public void Disconnect() {
        if (Client.IsConnected) {
            if (IsConnectionPoolingEnabled) {
                var identity = _poolIdentity ?? GetConnectionPoolIdentity();
                SmtpConnectionPool.ReturnClient(Server, Port, Client, identity, IsConnectionPoolingEnabled);
                Client = ClientFactory(Logging?.ProtocolLogger);
            } else {
                Client.Disconnect(true);
            }
        }
        Stopwatch.Stop();
    }

    /// <summary>
    /// Releases the SMTP connection and associated resources.
    /// </summary>
    public void Dispose() {
        var clientToDispose = Client;
        if (Client.IsConnected) {
            if (IsConnectionPoolingEnabled) {
                var identity = _poolIdentity ?? GetConnectionPoolIdentity();
                SmtpConnectionPool.ReturnClient(Server, Port, Client, identity, IsConnectionPoolingEnabled);
                Client = ClientFactory(Logging?.ProtocolLogger);
                clientToDispose = Client;
            } else {
                Client.Disconnect(true);
            }
        }
        clientToDispose.Dispose();
        Stopwatch.Stop();
    }
}
