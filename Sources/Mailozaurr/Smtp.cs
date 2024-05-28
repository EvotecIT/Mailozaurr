using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr;

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

    public List<string>? Attachments {
        get => Client.Attachments;
        set => Client.Attachments = value;
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

    public bool CheckCertificateRevocation {
        get => Client.CheckCertificateRevocation;
        set => Client.CheckCertificateRevocation = value;
    }

    //public bool SkipCertificateValidation {
    //    get
    //    {

    //    };
    //    set
    //    {

    //    };
    //}

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
    public string SentFrom => Client.SentFrom;

    public readonly Stopwatch Stopwatch;

    /// <summary>
    /// Initializes a new instance of the <see cref="Smtp"/> class, with optional logging configuration.
    /// </summary>
    /// <param name="logging">The logging.</param>
    public Smtp(LoggingConfigurator? logging = null) {
        Logging = logging;
        Client = logging?.ProtocolLogger == null ? new ClientSmtp() : new ClientSmtp(logging.ProtocolLogger);
        Stopwatch = Stopwatch.StartNew();
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

    public void CreateMessage() {
        Client.CreateMessage();
    }

    public void SaveMessage(string path) {
        if (!string.IsNullOrEmpty(path)) {
            Client.SaveMessage(path);
        }
    }

    /// <summary>
    /// Connect to the SMTP server using the provided server and port.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="port"></param>
    /// <param name="secureSocketOptions"></param>
    /// <param name="useSsl"></param>
    /// <returns></returns>
    public SmtpResult Connect(string server, int port, SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto, bool useSsl = false) {
        Server = server;
        Port = port;
        try {
            if (useSsl) {
                // If useSsl is true, use SecureSocketOptions.StartTls, otherwise use whatever is passed in secureSocketOptions
                // Since we are maintaining backwards compatibility with Send-MailMessage, we need to use StartTls if useSsl is true
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
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", "Could not authenticate using default credentials.");
        }
    }

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

    public SmtpResult Authenticate(string username, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        try {
            Client.Authenticate(username, password);
            LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Authenticated as {username}");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during authentication: {ex.Message}");
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Username? ({username} was used), Password?.");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// Send the email message.
    /// </summary>
    /// <returns></returns>
    public SmtpResult Send() {
        try {
            Client.Send(Message);
            LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Sent email to {SentTo}");
            return new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending: {ex.Message}");
            //LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Message? ({Message} was used).");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    public void Disconnect() {
        Client.Disconnect(true);
        Stopwatch.Stop();
    }

    public void Dispose() {
        Disconnect();
        Client.Dispose();
        Stopwatch.Stop();
    }

    public SmtpResult Encrypt(string pfxFilePath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        X509Certificate2 certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return Encrypt(certificate);
    }

    public SmtpResult Encrypt(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        store.Close();

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to encrypt the email
            return Encrypt(certificates[0]);
        } else {
            if (ErrorAction == ActionPreference.Stop) {
                throw new Exception("Certificate not found in the store.");
            }
            return new SmtpResult(true, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "Certificate not found in the store.");
        }
    }

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

    public SmtpResult Sign(string pfxFilePath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        X509Certificate2 certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return Sign(certificate);
    }

    public SmtpResult Sign(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        store.Close();

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to sign the email
            return Sign(certificates[0]);
        } else {
            throw new Exception("Certificate not found in the store.");
        }
    }

    public SmtpResult Pkcs7Sign(string pfxFilePath, string password, bool isSecureString) {
        password = ConvertSecureStringToPlainString(password, isSecureString);
        X509Certificate2 certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return Pkcs7Sign(certificate);
    }

    public SmtpResult Pkcs7Sign(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        store.Close();

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to sign the email
            return Pkcs7Sign(certificates[0]);
        } else {
            if (ErrorAction == ActionPreference.Stop) {
                throw new Exception("Certificate not found in the store.");
            }
            return new SmtpResult(true, EmailAction.SMimeSignaturePKCS7, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", "Certificate not found in the store.");
        }
    }

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
