using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Threading.Tasks;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// High level wrapper around <see cref="ClientSmtp"/> that exposes convenient methods and retry logic.
/// </summary>
public class Smtp {
    public LoggingConfigurator? Logging;

    public ClientSmtp Client { get; }

    public string Subject {
        get => Client.Subject;
        set => Client.Subject = value;
    }

    public string HtmlBody {
        get => Client.HtmlBody;
        set => Client.HtmlBody = value;
    }

    public string TextBody {
        get => Client.TextBody;
        set => Client.TextBody = value;
    }

    public List<object>? Attachments {
        get => Client.Attachments;
        set => Client.Attachments = value;
    }

    public List<object>? InlineAttachments {
        get => Client.InlineAttachments;
        set => Client.InlineAttachments = value;
    }

    public IDictionary<string, string>? Headers {
        get => Client.Headers;
        set => Client.Headers = value;
    }

    public object From {
        get => Client.From;
        set => Client.From = value;
    }

    public IEnumerable<object>? To {
        get => Client.To;
        set => Client.To = value;
    }

    public IEnumerable<object>? Cc {
        get => Client.Cc;
        set => Client.Cc = value;
    }

    public IEnumerable<object>? Bcc {
        get => Client.Bcc;
        set => Client.Bcc = value;
    }

    public object? ReplyTo {
        get => Client.ReplyTo;
        set => Client.ReplyTo = value;
    }

    public MimeMessage Message {
        get => Client.Message;
        set => Client.Message = value;
    }

    public MessagePriority Priority {
        get => Client.Priority;
        set => Client.Priority = value;
    }

    public DeliveryNotification[]? DeliveryNotificationOption {
        get => Client.DeliveryNotificationOption;
        set {
            if (value != null) {
                Client.DeliveryNotificationOption = value;
            }
        }
    }

    public int Timeout {
        get => Client.Timeout;
        set => Client.Timeout = value;
    }

    public int RetryCount { get; set; } = 0;

    public int RetryDelayMilliseconds { get; set; } = 0;

    public double RetryDelayBackoff { get; set; } = 1.0;

    /// <summary>
    /// When set to <see langword="true"/>, replaces local image references in
    /// <see cref="HtmlBody"/> with inline attachments.
    /// </summary>
    public bool AutoEmbedImages { get; set; } = false;

    /// <summary>
    /// Forces retries even when the encountered error is not considered
    /// transient. By default retries occur only for transient failures.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    public string? WebhookUrl { get; set; }

    public bool CheckCertificateRevocation {
        get => Client.CheckCertificateRevocation;
        set => Client.CheckCertificateRevocation = value;
    }

    private bool _skipCertificateValidation;
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

    public string LocalDomain {
        get => Client.LocalDomain;
        set {
            if (value != "") Client.LocalDomain = value;
        }
    }

    public DeliveryStatusNotificationType? DeliveryStatusNotificationType {
        get => Client.DeliveryStatusNotificationType;
        set {
            if (value != null) {
                Client.DeliveryStatusNotificationType = value.Value;
            }
        }
    }

    public RemoteCertificateValidationCallback ServerCertificateValidationCallback {
        get => Client.ServerCertificateValidationCallback;
        set => Client.ServerCertificateValidationCallback = value;
    }

    public int Port { get; private set; } = 25;

    public string Server { get; private set; } = String.Empty;

    public ActionPreference? ErrorAction { get; set; }

    public string SentTo => Client.SentTo;
    public string SentFrom => Helpers.GetEmailAddress(From);

    public readonly Stopwatch Stopwatch;

    /// <summary>
    /// Initializes a new instance of the <see cref="Smtp"/> class, with optional logging configuration.
    /// </summary>
    /// <param name="logging">The logging.</param>
    public Smtp(LoggingConfigurator? logging = null) {
        Stopwatch = Stopwatch.StartNew();
        Logging = logging;
        Client = logging?.ProtocolLogger == null ? new ClientSmtp() : new ClientSmtp(logging.ProtocolLogger);
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

        LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Logging configuration: Path: {logPath}, Console: {logConsole}, Object: {logObject}, Timestamps: {logTimestamps}, Secrets: {logSecrets}, TimestampsFormat: {logTimestampsFormat}, ServerPrefix: {logServerPrefix}, ClientPrefix: {logClientPrefix}, Overwrite: {logOverwrite}");
        Logging = new LoggingConfigurator();
        Logging.ConfigureLogging(logPath, logConsole, logObject, logTimestamps, logSecrets, logTimestampsFormat, logServerPrefix, logClientPrefix, logOverwrite);
        Client = Logging.ProtocolLogger == null ? new ClientSmtp() : new ClientSmtp(Logging.ProtocolLogger);
        Stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Creates the MIME message using the current property values.
    /// </summary>
    public void CreateMessage() {
        if (AutoEmbedImages) {
            var (html, paths) = HtmlUtils.ExtractLocalImagePaths(HtmlBody);
            HtmlBody = html;
            if (paths.Count > 0) {
                InlineAttachments ??= new List<object>();
                foreach (var p in paths) {
                    if (!InlineAttachments.Contains(p)) {
                        InlineAttachments.Add(p);
                    }
                }
            }
        }
        Client.CreateMessage();
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
        Server = server;
        Port = port;
        try {
            if (useSsl && secureSocketOptions == SecureSocketOptions.Auto) {
                // Maintain backwards compatibility with Send-MailMessage by
                // defaulting to StartTls when the UseSsl flag is supplied and
                // no explicit option was provided.
                secureSocketOptions = SecureSocketOptions.StartTls;
            }
            Client.Connect(server, port, secureSocketOptions);
            LoggingMessages.Logger.WriteVerbose($"Connected to {server} on {port} port using SSL: {secureSocketOptions}");
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, server, port, Stopwatch.Elapsed, "");
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during connect: {ex.Message}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Port? ({port} was used), Using SSL? ({secureSocketOptions}, was used). You can also try 'SkipCertificateValidation' or 'SkipCertificateRevocation'.");
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
        try {
            if (isOAuth) {
                var networkCredential = Credentials as NetworkCredential;
                if (networkCredential != null) {
                    var (userName, token) = Helpers.ConvertFromOAuth2Credential(networkCredential);
                    var oauth2 = new SaslMechanismOAuth2(userName, token);
                    Client.Authenticate(oauth2);
                    //  Settings.Logger.WriteVerbose($"Send-EmailMessage - Authenticated using OAuth");
                }
                LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Authenticated using oAuth");
            } else {
                Client.Authenticate(Credentials);
                //  Settings.Logger.WriteVerbose($"Send-EmailMessage - Authenticated using ICredentials");
            }
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during authentication (oAuth): {ex.Message}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: OAuth? ({isOAuth} was used), ICredentials? ({Credentials}, was used).");
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
        try {
            var mechanism = new SaslMechanismNtlmIntegrated();
            Client.Authenticate(mechanism);
            LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Authenticated using default credentials");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Could not authenticate using default credentials. Error: {ex.Message}");
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
                return Marshal.PtrToStringUni(unmanagedString);
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
    /// <returns>An <see cref="SmtpResult"/> representing the outcome.</returns>
    public SmtpResult Authenticate(string username, string password, bool isSecureString, AuthenticationMechanism mechanism = AuthenticationMechanism.Plain) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
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
            LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Authenticated as {username}");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during authentication: {ex.Message}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Username? ({username} was used), Password?.");
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
    /// <returns></returns>
    public SmtpResult Send() {
        return SendCoreAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Send the email message asynchronously.
    /// </summary>
    /// <returns></returns>
    public Task<SmtpResult> SendAsync(CancellationToken cancellationToken = default) {
        return SendCoreAsync(cancellationToken);
    }

    private async Task<SmtpResult> SendCoreAsync(CancellationToken cancellationToken = default) {
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                await Client.SendAsync(Message, cancellationToken);
                LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Sent email to {SentTo}");
                var result = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
                await Helpers.PostWebhookAsync(WebhookUrl, result, cancellationToken);
                return result;
            } catch (Exception ex) {
                lastException = ex;
                LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }

                var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delayMilliseconds > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);

        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return finalResult;
    }


    /// <summary>
    /// Disconnects from the SMTP server.
    /// </summary>
    public void Disconnect() {
        if (Client.IsConnected) {
            Client.Disconnect(true);
        }
        Stopwatch.Stop();
    }

    /// <summary>
    /// Releases the SMTP connection and associated resources.
    /// </summary>
    public void Dispose() {
        if (Client.IsConnected) {
            Client.Disconnect(true);
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
        // encrypt our message body using our custom S/MIME cryptography context
        using (var ctx = new DefaultSecureMimeContext()) {
            try {
                // Create a CmsRecipientCollection and add the CmsRecipient to it
                var recipients = new CmsRecipientCollection();
                recipients.Add(new CmsRecipient(certificate));

                // Encrypt the message body with the certificate
                message.Body = ApplicationPkcs7Mime.Encrypt(ctx, recipients, message.Body);
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during encryption: {ex.Message}");
                LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
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
        // digitally sign our message body using our custom S/MIME cryptography context
        //Exception calling "MultipartSignFromStore" with "1" argument(s): "SQLite is not available. Install the System.Data.SQLite nuget package."
        using (var ctx = new DefaultSecureMimeContext()) {
            try {
                var signer = new CmsSigner(certificate) {
                    DigestAlgorithm = DigestAlgorithm.Sha1
                };
                message.Body = MultipartSigned.Create(ctx, signer, message.Body);
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during signing: {ex.Message}");
                LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
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
        LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - {messageText}");
        LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Thumbprint '{certificateThumbprint}' is invalid or the certificate is missing.");

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
            // digitally sign our message body using our custom S/MIME cryptography context
            using (var ctx = new DefaultSecureMimeContext()) {
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
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
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

    public SmtpResult PgpEncrypt(string publicKeyPath) {
        if (!File.Exists(publicKeyPath)) {
            string messageText = $"Public key file not found: {publicKeyPath}";
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - {messageText}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Path '{publicKeyPath}' is invalid. Verify the file exists and the path is correct.");
            return new SmtpResult(false, EmailAction.PgpEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
        }

        MimeMessage message = Message;
        using var ctx = new EphemeralOpenPgpContext();
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

    public SmtpResult PgpSign(string publicKeyPath, string privateKeyPath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            MimeMessage message = Message;
            using var ctx = new EphemeralOpenPgpContext(password);
        if (!File.Exists(publicKeyPath)) {
            string messageText = $"Public key file not found: {publicKeyPath}";
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - {messageText}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Path '{publicKeyPath}' is invalid. Verify the file exists and the path is correct.");
            return new SmtpResult(false, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
        }
        if (!File.Exists(privateKeyPath)) {
            string messageText = $"Private key file not found: {privateKeyPath}";
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - {messageText}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Path '{privateKeyPath}' is invalid. Verify the file exists and the path is correct.");
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
        } finally {
            if (isSecureString) {
                using var securePwd = SecureStringHelper.FromPlainTextString(password);
                password = SecureStringHelper.Protect(securePwd);
            } else {
                password = new string('\0', password.Length);
            }
        }
    }

    public SmtpResult PgpSignAndEncrypt(string publicKeyPath, string privateKeyPath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            MimeMessage message = Message;
            using var ctx = new EphemeralOpenPgpContext(password);
        if (!File.Exists(publicKeyPath)) {
            string messageText = $"Public key file not found: {publicKeyPath}";
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - {messageText}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Path '{publicKeyPath}' is invalid. Verify the file exists and the path is correct.");
            return new SmtpResult(false, EmailAction.PgpSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
        }
        if (!File.Exists(privateKeyPath)) {
            string messageText = $"Private key file not found: {privateKeyPath}";
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - {messageText}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Path '{privateKeyPath}' is invalid. Verify the file exists and the path is correct.");
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
