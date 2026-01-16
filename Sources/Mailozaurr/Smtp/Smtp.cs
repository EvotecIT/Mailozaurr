using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Threading.Tasks;
using System.Threading;
using Mailozaurr.Definitions;

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
public class Smtp {

    /// <summary>Factory used to create <see cref="ClientSmtp"/> instances.</summary>
    public static Func<ProtocolLogger?, ClientSmtp> ClientFactory { get; set; } = logger => logger == null ? new ClientSmtp() : new ClientSmtp(logger);
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
    /// Optional identity hint used to isolate SMTP connection pooling by credentials.
    /// </summary>
    public string? ConnectionPoolIdentity { get; set; }

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
        get => Client.LocalDomain;
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
    public RemoteCertificateValidationCallback ServerCertificateValidationCallback {
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
    public static SmtpConnectionInfo TestConnection(string server, int port, SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto, bool useSsl = false)
    {
        var logging = new LoggingConfigurator();
        logging.ConfigureLogging(null, false, true, false, false);

        var smtp = new Smtp(logging);
        _ = smtp.Connect(server, port, secureSocketOptions, useSsl);

        string? banner = null;
        string? software = null;

        if (logging.LogStream != null)
        {
            logging.LogStream.Position = 0;
            using var reader = new StreamReader(logging.LogStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("<--", StringComparison.Ordinal))
                {
                    trimmed = trimmed.Substring(3).Trim();
                }
                banner = trimmed;
                var parts = trimmed.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    software = parts[2];
                }
            }
        }

        var persistent = false;
        try
        {
            smtp.Client.NoOp();
            persistent = smtp.Client.IsConnected;
        }
        catch
        {
            persistent = false;
        }

        var info = new SmtpConnectionInfo(server, port, banner, software, smtp.Client.Capabilities, persistent);
        smtp.Disconnect();
        smtp.Dispose();
        return info;
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
        if (Client.IsConnected)
        {
            if (SmtpConnectionPool.PoolingEnabled)
            {
                SmtpConnectionPool.ReturnClient(oldServer, oldPort, Client, oldPoolIdentity);
            }
            else
            {
                Client.Disconnect(true);
            }
            Client = ClientFactory(Logging?.ProtocolLogger);
        }

        var poolIdentity = GetConnectionPoolIdentity();
        var pooled = SmtpConnectionPool.PoolingEnabled ? SmtpConnectionPool.TryRentClient(server, port, poolIdentity) : null;
        if (pooled != null)
        {
            Client = pooled;
        }
        try {
            if (!Client.IsConnected)
            {
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
    public async Task<SmtpResult> ConnectAsync(string server, int port, SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto, bool useSsl = false) {
        var oldServer = Server;
        var oldPort = Port;
        var oldPoolIdentity = _poolIdentity ?? GetConnectionPoolIdentity();
        Server = server;
        Port = port;
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
        if (Client.IsConnected)
        {
            if (SmtpConnectionPool.PoolingEnabled)
            {
                SmtpConnectionPool.ReturnClient(oldServer, oldPort, Client, oldPoolIdentity);
            }
            else
            {
                Client.Disconnect(true);
            }
            Client = ClientFactory(Logging?.ProtocolLogger);
        }

        var poolIdentity = GetConnectionPoolIdentity();
        var pooled = SmtpConnectionPool.PoolingEnabled ? SmtpConnectionPool.TryRentClient(server, port, poolIdentity) : null;
        if (pooled != null)
        {
            Client = pooled;
        }
        try {
            if (!Client.IsConnected)
            {
                await Client.ConnectAsync(server, port, effectiveOptions);
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
        try {
            if (isOAuth) {
                var networkCredential = Credentials as NetworkCredential;
                if (networkCredential != null) {
                    Credential = networkCredential;
                    var (userName, token) = Helpers.ConvertFromOAuth2Credential(networkCredential);
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
    /// Send the email message.
    /// </summary>
    /// <remarks>
    /// Concurrent calls are serialized so that only one send executes at a time for a
    /// given instance.
    /// </remarks>
    /// <returns></returns>
    public SmtpResult Send() {
        _sendLock.Wait();
        try {
            return SendCoreAsync().GetAwaiter().GetResult();
        } finally {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Send the email message asynchronously.
    /// </summary>
    /// <remarks>
    /// Concurrent calls are serialized so that only one send executes at a time for a
    /// given instance.
    /// </remarks>
    /// <returns></returns>
    public async Task<SmtpResult> SendAsync(CancellationToken cancellationToken = default) {
        await _sendLock.WaitAsync(cancellationToken);
        try {
            return await SendCoreAsync(cancellationToken);
        } finally {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Attempts to send all messages stored in <see cref="PendingMessageRepository"/>.
    /// </summary>
    /// <remarks>
    /// Messages are removed from the repository only when sending succeeds. On
    /// success the message is also logged via <see cref="SentMessageRepository"/>,
    /// if configured.
    /// </remarks>
    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default) {
        if (PendingMessageRepository == null) {
            return;
        }

        await foreach (var record in PendingMessageRepository.GetAllAsync(cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (DryRun) {
                LogVerbose($"ProcessPendingMessages - DryRun enabled, skipping {record.MessageId}");
                continue;
            }
            if (string.IsNullOrWhiteSpace(record.MimeMessage) || string.IsNullOrEmpty(record.MessageId)) {
                continue;
            }
            if (record.NextAttemptAt > DateTimeOffset.UtcNow) {
                continue;
            }

            if (record.Provider != EmailProvider.None) {
                continue;
            }

            MimeMessage message;
            try {
                var bytes = Convert.FromBase64String(record.MimeMessage);
                using var ms = new MemoryStream(bytes);
                message = await MimeMessage.LoadAsync(ms, cancellationToken);
            } catch (Exception ex) {
                LogWarning($"ProcessPendingMessages - Failed to parse {record.MessageId}: {ex.Message}");
                continue;
            }

            var originalSkipValidation = SkipCertificateValidation;
            var originalCheckRevocation = CheckCertificateRevocation;
            var originalTimeout = Timeout;
            var originalSecureOptions = _activeSecureSocketOptions;
            var originalUseSsl = _activeUseSsl;
            var originalPoolIdentity = ConnectionPoolIdentity;
            var secureSocketOptions = _activeSecureSocketOptions;
            var useSsl = _activeUseSsl;
            if (record.ProviderData != null && record.ProviderData.Count > 0) {
                if (record.ProviderData.TryGetValue(ProviderDataSecureSocketOptionsKey, out var secureValue)
                    && Enum.TryParse(secureValue, out SecureSocketOptions parsedSecure)) {
                    secureSocketOptions = parsedSecure;
                }
                if (record.ProviderData.TryGetValue(ProviderDataUseSslKey, out var useSslValue)
                    && bool.TryParse(useSslValue, out var parsedUseSsl)) {
                    useSsl = parsedUseSsl;
                }
                if (record.ProviderData.TryGetValue(ProviderDataSkipCertificateValidationKey, out var skipValue)
                    && bool.TryParse(skipValue, out var parsedSkip)) {
                    SkipCertificateValidation = parsedSkip;
                }
                if (record.ProviderData.TryGetValue(ProviderDataCheckCertificateRevocationKey, out var revocationValue)
                    && bool.TryParse(revocationValue, out var parsedRevocation)) {
                    CheckCertificateRevocation = parsedRevocation;
                }
                if (record.ProviderData.TryGetValue(ProviderDataTimeoutKey, out var timeoutValue)
                    && int.TryParse(timeoutValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)) {
                    Timeout = timeout;
                }
            }
            if (!string.IsNullOrWhiteSpace(record.UserName)) {
                ConnectionPoolIdentity = record.UserName;
            }

            try {
                var server = record.Server ?? Server;
                var port = record.Port ?? Port;
                if (!string.IsNullOrWhiteSpace(server)) {
                    Connect(server, port, secureSocketOptions, useSsl);
                    if (!string.IsNullOrEmpty(record.UserName)) {
                        var pwd = CredentialProtection.UnprotectWithFallback(record.Password);
                        var cred = Helpers.ConvertFromPlainText(record.UserName!, pwd);
                        Authenticate(cred);
                    }
                }

                await Client.SendAsync(message, cancellationToken);
                LogVerbose($"Send-EmailMessage - Sent email to {message.To}");
                if (SentMessageRepository != null) {
                    var sentRecord = new SentMessageRecord {
                        MessageId = message.MessageId ?? record.MessageId,
                        Recipients = message.To.ToString(),
                        Subject = message.Subject ?? string.Empty,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    await SentMessageRepository.SaveAsync(sentRecord, cancellationToken);
                }
                await PendingMessageRepository.RemoveAsync(record.MessageId!, cancellationToken);
            } catch (Exception ex) {
                LogWarning($"ProcessPendingMessages - Error sending {record.MessageId}: {ex.Message}");
                var attempt = record.IncrementAttemptCount();
                var delay = CalculateRetryDelay(attempt - 1);
                record.NextAttemptAt = delay > TimeSpan.Zero
                    ? DateTimeOffset.UtcNow.Add(delay)
                    : DateTimeOffset.UtcNow;
                await PendingMessageRepository.SaveAsync(record, cancellationToken);
            } finally {
                Disconnect();
                SkipCertificateValidation = originalSkipValidation;
                CheckCertificateRevocation = originalCheckRevocation;
                Timeout = originalTimeout;
                _activeSecureSocketOptions = originalSecureOptions;
                _activeUseSsl = originalUseSsl;
                ConnectionPoolIdentity = originalPoolIdentity;
            }
        }
    }

    /// <summary>
    /// Logs a verbose message using LogCollector if available, otherwise uses LoggingMessages.Logger.
    /// </summary>
    private void LogVerbose(string message) {
        if (LogCollector != null) {
            LogCollector.LogVerbose(message);
        } else {
            LoggingMessages.Logger.WriteVerbose(message);
        }
    }

    /// <summary>
    /// Logs a warning message using LogCollector if available, otherwise uses LoggingMessages.Logger.
    /// </summary>
    private void LogWarning(string message) {
        if (LogCollector != null) {
            LogCollector.LogWarning(message);
        } else {
            LoggingMessages.Logger.WriteWarning(message);
        }
    }

    private Dictionary<string, string> CreateProviderDataSnapshot() {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            [ProviderDataSecureSocketOptionsKey] = _activeSecureSocketOptions.ToString(),
            [ProviderDataUseSslKey] = _activeUseSsl.ToString(CultureInfo.InvariantCulture),
            [ProviderDataSkipCertificateValidationKey] = _skipCertificateValidation.ToString(CultureInfo.InvariantCulture),
            [ProviderDataCheckCertificateRevocationKey] = Client.CheckCertificateRevocation.ToString(CultureInfo.InvariantCulture),
            [ProviderDataTimeoutKey] = Client.Timeout.ToString(CultureInfo.InvariantCulture)
        };

        return data;
    }

    private string GetConnectionPoolIdentity() {
        var userName = ConnectionPoolIdentity;
        var domain = string.Empty;
        if (string.IsNullOrWhiteSpace(userName)) {
            userName = Credential?.UserName;
            domain = Credential?.Domain ?? string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(domain)) {
            userName = string.IsNullOrWhiteSpace(userName) ? domain : $"{domain}\\{userName}";
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            userName = "anonymous";
        }
        return $"{userName}|{_activeSecureSocketOptions}|{_activeUseSsl}";
    }

    private TimeSpan CalculateRetryDelay(int attempt) {
        if (attempt < 0) attempt = 0;
        var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempt));
        if (MaxDelayMilliseconds > 0 && delayMilliseconds > MaxDelayMilliseconds) {
            delayMilliseconds = MaxDelayMilliseconds;
        }
        if (JitterMilliseconds > 0 && delayMilliseconds > 0) {
            delayMilliseconds += GraphRetryHelperRandom.NextInt(JitterMilliseconds + 1);
        }
        return delayMilliseconds > 0
            ? TimeSpan.FromMilliseconds(delayMilliseconds)
            : TimeSpan.Zero;
    }

    private string EnsureMessageId() {
        var id = Message.MessageId;
        if (string.IsNullOrEmpty(id)) {
            Message.MessageId = id = MimeKit.Utils.MimeUtils.GenerateMessageId();
        }
        return id;
    }

    private async Task SaveSentMessageAsync(string messageId, CancellationToken cancellationToken) {
        if (SentMessageRepository == null) {
            return;
        }

        var record = new SentMessageRecord {
            MessageId = messageId,
            Recipients = SentTo,
            Subject = Subject,
            Timestamp = DateTimeOffset.UtcNow
        };
        await SentMessageRepository.SaveAsync(record, cancellationToken);
    }

    private async Task RemovePendingMessageAsync(string? messageId, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null || string.IsNullOrEmpty(messageId)) {
            return;
        }

        var safeMessageId = messageId!;
        await PendingMessageRepository.RemoveAsync(safeMessageId, cancellationToken);
    }

    private async Task EnqueuePendingMessageAsync(string messageId, ICredentialProtector credentialProtector, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null) {
            return;
        }

        using var ms = new MemoryStream();
        await Message.WriteToAsync(ms, cancellationToken);
        var record = new PendingMessageRecord {
            MessageId = messageId,
            MimeMessage = Convert.ToBase64String(ms.ToArray()),
            Timestamp = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow,
            Provider = EmailProvider.None,
            Server = Server,
            Port = Port,
            UserName = Credential?.UserName,
            Password = string.IsNullOrEmpty(Credential?.Password)
                ? null
                : credentialProtector.Protect(Credential!.Password),
            ProviderData = CreateProviderDataSnapshot()
        };
        await PendingMessageRepository.SaveAsync(record, cancellationToken);
    }

    private async Task<SmtpResult> SendCoreAsync(CancellationToken cancellationToken = default) {
        if (DryRun) {
            LogVerbose("Send-EmailMessage - DryRun enabled, skipping send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)") {
                MessageId = Message?.MessageId
            };
        }
        int attempts = 0;
        Exception? lastException = null;
        var credentialProtector = CredentialProtection.Default;

        do {
            try {
                await Client.SendAsync(Message, cancellationToken);
                LogVerbose($"Send-EmailMessage - Sent email to {SentTo}");
                await SaveSentMessageAsync(Message.MessageId ?? string.Empty, cancellationToken);
                await RemovePendingMessageAsync(Message.MessageId, cancellationToken);
                var result = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging) {
                    MessageId = Message.MessageId
                };
                await Helpers.PostWebhookAsync(WebhookUrl, result, cancellationToken);
                return result;
            } catch (Exception ex) {
                lastException = ex;
                LogWarning($"Send-EmailMessage - Error during sending: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var id = EnsureMessageId();
                    await EnqueuePendingMessageAsync(id, credentialProtector, cancellationToken);
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message) {
                        MessageId = id
                    };
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }

                var delay = CalculateRetryDelay(attempts);
                if (delay > TimeSpan.Zero) {
                    await Task.Delay(delay, cancellationToken);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);

        var finalId = EnsureMessageId();
        await EnqueuePendingMessageAsync(finalId, credentialProtector, cancellationToken);
        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", lastException?.Message) {
            MessageId = finalId
        };
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return finalResult;
    }


    /// <summary>
    /// Disconnects from the SMTP server.
    /// </summary>
    public void Disconnect() {
        if (Client.IsConnected) {
            if (SmtpConnectionPool.PoolingEnabled) {
                var identity = _poolIdentity ?? GetConnectionPoolIdentity();
                SmtpConnectionPool.ReturnClient(Server, Port, Client, identity);
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
        if (Client.IsConnected) {
            if (SmtpConnectionPool.PoolingEnabled) {
                var identity = _poolIdentity ?? GetConnectionPoolIdentity();
                SmtpConnectionPool.ReturnClient(Server, Port, Client, identity);
            } else {
                Client.Disconnect(true);
            }
        }
        Client.Dispose();
        Stopwatch.Stop();
    }

    /// <summary>
    /// S/MIME encrypt the message using a PFX certificate file.
    /// </summary>
    /// <param name="pfxFilePath">Path to the PFX file.</param>
    /// <param name="password">Certificate password.</param>
    /// <param name="isSecureString">Indicates if the password is protected.</param>
    /// <returns></returns>
    public SmtpResult Encrypt(string pfxFilePath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            using var certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            return Encrypt(certificate);
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
    /// S/MIME encrypt the message using a certificate from the store.
    /// </summary>
    /// <param name="certificateThumbprint">Certificate thumbprint.</param>
    /// <returns></returns>
    public SmtpResult Encrypt(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to encrypt the email
            return Encrypt(certificates[0]);
        } else {
            if (ErrorAction == ActionPreference.Stop) {
                throw new Exception("Certificate not found in the store.");
            }
            return new SmtpResult(false, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", "Certificate not found in the store.");
        }
    }

    /// <summary>
    /// S/MIME encrypt the message using the specified certificate instance.
    /// </summary>
    /// <param name="certificate">Certificate to encrypt with.</param>
    /// <returns></returns>
    public SmtpResult Encrypt(X509Certificate2 certificate) {
        MimeMessage message = Message;
        // encrypt our message body using a temporary S/MIME context to avoid SQLite dependency
        using (var ctx = new TemporarySecureMimeContext()) {
            try {
                // Create a CmsRecipientCollection and add the CmsRecipient to it
                var recipients = new CmsRecipientCollection();
                recipients.Add(new CmsRecipient(certificate));

                // Encrypt the message body with the certificate
                message.Body = ApplicationPkcs7Mime.Encrypt(ctx, recipients, message.Body);
            } catch (Exception ex) {
                LogWarning($"Send-EmailMessage - Error during encryption: {ex.Message}");
                LogWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
                if (ErrorAction == ActionPreference.Stop) {
                    throw;
                }
                return new SmtpResult(false, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
            }
        }

        Message = message;
        return new SmtpResult(true, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
    }

    /// <summary>
    /// S/MIME sign the message using the specified certificate.
    /// </summary>
    /// <param name="certificate">Certificate used for signing.</param>
    /// <returns></returns>
    public SmtpResult Sign(X509Certificate2 certificate) {
        MimeMessage message = Message;
        // digitally sign our message body using a temporary S/MIME context
        // TemporarySecureMimeContext avoids the SQLite dependency of DefaultSecureMimeContext
        using (var ctx = new TemporarySecureMimeContext()) {
            try {
                var signer = new CmsSigner(certificate) {
                    DigestAlgorithm = DigestAlgorithm.Sha1
                };
                message.Body = MultipartSigned.Create(ctx, signer, message.Body);
            } catch (Exception ex) {
                LogWarning($"Send-EmailMessage - Error during signing: {ex.Message}");
                LogWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
                if (ErrorAction == ActionPreference.Stop) {
                    throw;
                }
                return new SmtpResult(false, EmailAction.SMimeSignature, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
            }
        }
        Message = message;
        return new SmtpResult(true, EmailAction.SMimeSignature, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
    }

    /// <summary>
    /// S/MIME sign the message using a PFX certificate file.
    /// </summary>
    /// <param name="pfxFilePath">Path to the PFX file.</param>
    /// <param name="password">Certificate password.</param>
    /// <param name="isSecureString">Indicates if the password is protected.</param>
    /// <returns></returns>
    public SmtpResult Sign(string pfxFilePath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            using var certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            return Sign(certificate);
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
    /// S/MIME sign the message using a certificate from the store.
    /// </summary>
    /// <param name="certificateThumbprint">Certificate thumbprint.</param>
    /// <returns></returns>
    public SmtpResult Sign(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to sign the email
            return Sign(certificates[0]);
        }

        var messageText = "Certificate not found in the store.";
        LogWarning($"Send-EmailMessage - {messageText}");
        LogWarning($"Send-EmailMessage - Possible issue: Thumbprint '{certificateThumbprint}' is invalid or the certificate is missing.");

        if (ErrorAction == ActionPreference.Stop) {
            throw new Exception(messageText);
        }

        return new SmtpResult(false, EmailAction.SMimeSignature, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
    }

    /// <summary>
    /// PKCS#7 sign the message using a PFX certificate file.
    /// </summary>
    /// <param name="pfxFilePath">Path to the PFX file.</param>
    /// <param name="password">Certificate password.</param>
    /// <param name="isSecureString">Indicates if the password is protected.</param>
    /// <returns></returns>
    public SmtpResult Pkcs7Sign(string pfxFilePath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            using var certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            return Pkcs7Sign(certificate);
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
    /// PKCS#7 sign the message using a certificate from the store.
    /// </summary>
    /// <param name="certificateThumbprint">Certificate thumbprint.</param>
    /// <returns></returns>
    public SmtpResult Pkcs7Sign(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to sign the email
            return Pkcs7Sign(certificates[0]);
        } else {
            if (ErrorAction == ActionPreference.Stop) {
                throw new Exception("Certificate not found in the store.");
            }
            return new SmtpResult(false, EmailAction.SMimeSignaturePKCS7, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", "Certificate not found in the store.");
        }
    }

    /// <summary>
    /// PKCS#7 sign the message using the specified certificate.
    /// </summary>
    /// <param name="certificate">Certificate used for signing.</param>
    /// <returns></returns>
    public SmtpResult Pkcs7Sign(X509Certificate2 certificate) {
        try {
            MimeMessage message = Message;
            // digitally sign our message body using a temporary S/MIME context to avoid SQLite dependency
            using (var ctx = new TemporarySecureMimeContext()) {
                // Create a signer with the certificate
                var signer = new CmsSigner(certificate) {
                    DigestAlgorithm = DigestAlgorithm.Sha256
                };

                // Sign the message body with the signer
                message.Body = ApplicationPkcs7Mime.Sign(ctx, signer, message.Body);
            }

            Message = message;
            return new SmtpResult(true, EmailAction.SMimeSignaturePKCS7, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LogWarning($"Send-EmailMessage - Error: {ex.Message}");
            LogWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.SMimeSignaturePKCS7, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// S/MIME Sign and encrypt the email using the provided certificate thumbprint.
    /// </summary>
    /// <param name="certificateThumbprint"></param>
    /// <returns></returns>
    public SmtpResult SignAndEncrypt(string certificateThumbprint) {
        // Sign the email
        SmtpResult signResult = Sign(certificateThumbprint);
        if (!signResult.Status) {
            return signResult;
        }

        // Encrypt the signed email
        SmtpResult encryptResult = Encrypt(certificateThumbprint);
        if (!encryptResult.Status) {
            return encryptResult;
        }

        return new SmtpResult(true, EmailAction.SMimeSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
    }

    /// <summary>
    /// S/MIME Sign and encrypt the email using the provided PFX file and password.
    /// </summary>
    /// <param name="pfxFilePath"></param>
    /// <param name="password"></param>
    /// <param name="isSecureString"></param>
    /// <returns></returns>
    public SmtpResult SignAndEncrypt(string pfxFilePath, string password, bool isSecureString) {
        // Sign the email
        SmtpResult signResult = Sign(pfxFilePath, password, isSecureString);
        if (!signResult.Status) {
            return signResult;
        }

        // Encrypt the signed email
        SmtpResult encryptResult = Encrypt(pfxFilePath, password, isSecureString);
        if (!encryptResult.Status) {
            return encryptResult;
        }

        return new SmtpResult(true, EmailAction.SMimeSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
    }

    /// <summary>
    /// Performs the specified S/MIME action using the provided certificate.
    /// </summary>
    /// <param name="emailActionEncryption">The operation to perform.</param>
    /// <param name="certificate">Certificate instance.</param>
    public SmtpResult Encrypt(EmailActionEncryption emailActionEncryption, X509Certificate2 certificate) {
        return emailActionEncryption switch {
            EmailActionEncryption.SMIMESign => Sign(certificate),
            EmailActionEncryption.SMIMESignPkcs7 => Pkcs7Sign(certificate),
            EmailActionEncryption.SMIMEEncrypt => Encrypt(certificate),
            EmailActionEncryption.SMIMESignAndEncrypt => SignAndEncrypt(certificate),
            _ => new SmtpResult(true, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", "EmailActionEncryption None")
        };
    }

    /// <summary>
    /// S/MIME Sign and encrypt the email using the provided certificate.
    /// </summary>
    /// <param name="certificate">Certificate to use.</param>
    public SmtpResult SignAndEncrypt(X509Certificate2 certificate) {
        SmtpResult signResult = Sign(certificate);
        if (!signResult.Status) {
            return signResult;
        }

        SmtpResult encryptResult = Encrypt(certificate);
        if (!encryptResult.Status) {
            return encryptResult;
        }

        return new SmtpResult(true, EmailAction.SMimeSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
    }

    /// <summary>
    /// Encrypts the current message using the specified OpenPGP public key.
    /// </summary>
    /// <param name="publicKeyPath">Path to the recipient public key.</param>
    /// <returns>The result of the encryption operation.</returns>
    public SmtpResult PgpEncrypt(string publicKeyPath) {
        if (!File.Exists(publicKeyPath)) {
            string messageText = $"Public key file not found: {publicKeyPath}";
            LogWarning($"Send-EmailMessage - {messageText}");
            LogWarning($"Send-EmailMessage - Possible issue: Path '{publicKeyPath}' is invalid. Verify the file exists and the path is correct.");
            return new SmtpResult(false, EmailAction.PgpEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
        }

        MimeMessage message = Message;
        using (var ctx = new EphemeralOpenPgpContext()) {
            using (var pub = File.OpenRead(publicKeyPath))
                ctx.Import(pub);
            var recipients = message.To.Mailboxes.Concat(message.Cc.Mailboxes).Concat(message.Bcc.Mailboxes).ToList();
            try {
                var keys = ctx.GetPublicKeys(recipients);
                message.Body = MultipartEncrypted.Encrypt(ctx, keys, message.Body);
            } catch (Exception ex) {
                if (ErrorAction == ActionPreference.Stop) throw;
                return new SmtpResult(false, EmailAction.PgpEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
            }
            Message = message;
            return new SmtpResult(true, EmailAction.PgpEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        }
    }

    /// <summary>
    /// Signs the current message using OpenPGP keys.
    /// </summary>
    /// <param name="publicKeyPath">Path to the public key.</param>
    /// <param name="privateKeyPath">Path to the private key.</param>
    /// <param name="password">Password protecting the private key.</param>
    /// <param name="isSecureString">Whether the password is protected.</param>
    /// <returns>The result of the signing operation.</returns>
    public SmtpResult PgpSign(string publicKeyPath, string privateKeyPath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            MimeMessage message = Message;
            using (var ctx = new EphemeralOpenPgpContext(password)) {
                if (!File.Exists(publicKeyPath)) {
                    string messageText = $"Public key file not found: {publicKeyPath}";
                    LogWarning($"Send-EmailMessage - {messageText}");
                    LogWarning($"Send-EmailMessage - Possible issue: Path '{publicKeyPath}' is invalid. Verify the file exists and the path is correct.");
                    return new SmtpResult(false, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
                }
                if (!File.Exists(privateKeyPath)) {
                    string messageText = $"Private key file not found: {privateKeyPath}";
                    LogWarning($"Send-EmailMessage - {messageText}");
                    LogWarning($"Send-EmailMessage - Possible issue: Path '{privateKeyPath}' is invalid. Verify the file exists and the path is correct.");
                    return new SmtpResult(false, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
                }

                using (var pub = File.OpenRead(publicKeyPath))
                    ctx.Import(pub);
                using (var sec = File.OpenRead(privateKeyPath))
                    ctx.Import(new PgpSecretKeyRingBundle(new Org.BouncyCastle.Bcpg.ArmoredInputStream(sec)));
                try {
                    var signer = message.From.Mailboxes.First();
                    var signingKey = ctx.GetSigningKey(signer);
                    message.Body = MultipartSigned.Create(ctx, signingKey, DigestAlgorithm.Sha256, message.Body);
                    var signed = (MultipartSigned)message.Body;
                    var sigs = signed.Verify(ctx);
                    foreach (var sig in sigs)
                        sig.Verify();
                } catch (Exception ex) {
                    if (ErrorAction == ActionPreference.Stop) throw;
                    return new SmtpResult(false, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
                }
                Message = message;
                return new SmtpResult(true, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
            }
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
    /// Signs and encrypts the current message using OpenPGP keys.
    /// </summary>
    /// <param name="publicKeyPath">Path to the public key.</param>
    /// <param name="privateKeyPath">Path to the private key.</param>
    /// <param name="password">Password protecting the private key.</param>
    /// <param name="isSecureString">Whether the password is protected.</param>
    /// <returns>The result of the sign and encrypt operation.</returns>
    public SmtpResult PgpSignAndEncrypt(string publicKeyPath, string privateKeyPath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            MimeMessage message = Message;
            using (var ctx = new EphemeralOpenPgpContext(password)) {
                if (!File.Exists(publicKeyPath)) {
                    string messageText = $"Public key file not found: {publicKeyPath}";
                    LogWarning($"Send-EmailMessage - {messageText}");
                    LogWarning($"Send-EmailMessage - Possible issue: Path '{publicKeyPath}' is invalid. Verify the file exists and the path is correct.");
                    return new SmtpResult(false, EmailAction.PgpSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
                }
                if (!File.Exists(privateKeyPath)) {
                    string messageText = $"Private key file not found: {privateKeyPath}";
                    LogWarning($"Send-EmailMessage - {messageText}");
                    LogWarning($"Send-EmailMessage - Possible issue: Path '{privateKeyPath}' is invalid. Verify the file exists and the path is correct.");
                    return new SmtpResult(false, EmailAction.PgpSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
                }

                using (var pub = File.OpenRead(publicKeyPath))
                    ctx.Import(pub);
                using (var sec = File.OpenRead(privateKeyPath))
                    ctx.Import(new PgpSecretKeyRingBundle(new Org.BouncyCastle.Bcpg.ArmoredInputStream(sec)));
                var recipients = message.To.Mailboxes.Concat(message.Cc.Mailboxes).Concat(message.Bcc.Mailboxes).ToList();
                try {
                    var signingKey = ctx.GetSigningKey(message.From.Mailboxes.First());
                    var encKeys = ctx.GetPublicKeys(recipients);
                    message.Body = MultipartEncrypted.SignAndEncrypt(ctx, signingKey, DigestAlgorithm.Sha256, EncryptionAlgorithm.Cast5, encKeys, message.Body);
                } catch (Exception ex) {
                    if (ErrorAction == ActionPreference.Stop) throw;
                    return new SmtpResult(false, EmailAction.PgpSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
                }
                Message = message;
                return new SmtpResult(true, EmailAction.PgpSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
            }
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
    /// Performs the specified S/MIME action using a PFX certificate file.
    /// </summary>
    /// <param name="emailActionEncryption">The operation to perform.</param>
    /// <param name="pfxFilePath">Path to the PFX file.</param>
    /// <param name="password">Certificate password.</param>
    /// <param name="isSecureString">Indicates if the password is protected.</param>
    /// <returns></returns>
    public SmtpResult Encrypt(EmailActionEncryption emailActionEncryption, string pfxFilePath, string password, bool isSecureString) {
        switch (emailActionEncryption) {
            case EmailActionEncryption.SMIMESign:
                return Sign(pfxFilePath, password, isSecureString);
            case EmailActionEncryption.SMIMESignPkcs7:
                return Pkcs7Sign(pfxFilePath, password, isSecureString);
            case EmailActionEncryption.SMIMEEncrypt:
                return Encrypt(pfxFilePath, password, isSecureString);
            case EmailActionEncryption.SMIMESignAndEncrypt:
                return SignAndEncrypt(pfxFilePath, password, isSecureString);
            default:
                // user did not specify an encryption type, we skip things
                return new SmtpResult(true, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", "EmailActionEncryption None");
        }
    }
    /// <summary>
    /// Performs the specified S/MIME action using a certificate from the store.
    /// </summary>
    /// <param name="emailActionEncryption">The operation to perform.</param>
    /// <param name="certificateThumbprint">Certificate thumbprint.</param>
    /// <returns></returns>
    public SmtpResult Encrypt(EmailActionEncryption emailActionEncryption, string certificateThumbprint) {
        switch (emailActionEncryption) {
            case EmailActionEncryption.SMIMESign:
                return Sign(certificateThumbprint);
            case EmailActionEncryption.SMIMESignPkcs7:
                return Pkcs7Sign(certificateThumbprint);
            case EmailActionEncryption.SMIMEEncrypt:
                return Encrypt(certificateThumbprint);
            case EmailActionEncryption.SMIMESignAndEncrypt:
                return SignAndEncrypt(certificateThumbprint);
            default:
                // user did not specify an encryption type, we skip things
                return new SmtpResult(true, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", "EmailActionEncryption None");
        }
    }
}
