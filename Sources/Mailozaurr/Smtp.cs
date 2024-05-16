using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Security;
using MimeKit.Cryptography;
using Org.BouncyCastle.Crypto.Signers;
using PgpCore;
using System.Text;

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

    public List<DeliveryNotification>? DeliveryNotificationOption {
        get => Client.DeliveryNotificationOption;
        set => Client.DeliveryNotificationOption = value;
    }

    public int Timeout {
        get => Client.Timeout;
        set => Client.Timeout = value;
    }

    public bool CheckCertificateRevocation {
        get => Client.CheckCertificateRevocation;
        set => Client.CheckCertificateRevocation = value;
    }

    public string LocalDomain {
        get => Client.LocalDomain;
        set {
            if (value != "") Client.LocalDomain = value;
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

    private readonly Stopwatch stopwatch;

    /// <summary>
    /// Initializes a new instance of the <see cref="Smtp"/> class, with optional logging configuration.
    /// </summary>
    /// <param name="logging">The logging.</param>
    public Smtp(LoggingConfigurator? logging = null) {
        Logging = logging;
        Client = logging?.ProtocolLogger == null ? new ClientSmtp() : new ClientSmtp(logging.ProtocolLogger);
        stopwatch = Stopwatch.StartNew();
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
        Logging = new LoggingConfigurator();
        Logging.ConfigureLogging(logPath, logConsole, logObject, logTimestamps, logSecrets, logTimestampsFormat,
                       logServerPrefix, logClientPrefix, logOverwrite);
        Client = Logging.ProtocolLogger == null ? new ClientSmtp() : new ClientSmtp(Logging.ProtocolLogger);
        stopwatch = Stopwatch.StartNew();


        //MySecureMimeContext.DatabasePath = "C:\\Temp\\certdb.sqlite";
        //CryptographyContext.Register(typeof(MySecureMimeContext));

        CryptographyContext.Register(typeof(MyGnuPGContext));
    }

    public void CreateMessage() {
        Client.CreateMessage();
    }

    public void SaveMessage(string path) {
        if (!string.IsNullOrEmpty(path)) {
            Client.SaveMessage(path);
        }
    }

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
            Settings.Logger.WriteVerbose($"Connected to {server} on {port} port using SSL: {secureSocketOptions}");
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, server, port, stopwatch.Elapsed, "");
        } catch (Exception ex) {
            Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
            Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Port? ({port} was used), Using SSL? ({secureSocketOptions}, was used). You can also try 'SkipCertificateValidation' or 'SkipCertificateRevocation'.");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, server, port, stopwatch.Elapsed, ex.Message);
        }
    }

    public SmtpResult Authenticate(ICredentials Credentials, bool isOAuth = false) {
        try {
            if (isOAuth) {
                var networkCredential = Credentials as NetworkCredential;
                if (networkCredential != null) {
                    var (userName, token) = Helpers.ConvertFromOAuth2Credential(networkCredential);
                    var oauth2 = new SaslMechanismOAuth2(userName, token);
                    Client.Authenticate(oauth2);
                    Settings.Logger.WriteVerbose($"Send-EmailMessage - Authenticated using OAuth");
                }
                Settings.Logger.WriteVerbose($"Send-EmailMessage - Authenticated using OAuth");
            } else {
                Client.Authenticate(Credentials);
                Settings.Logger.WriteVerbose($"Send-EmailMessage - Authenticated using ICredentials");
            }
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
            Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: OAuth? ({isOAuth} was used), ICredentials? ({Credentials}, was used).");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
        }
    }

    public SmtpResult Authenticate(string username, string password, bool isSecureString) {
        if (isSecureString) {

            // Convert the encrypted string back to a SecureString
            SecureString securePassword = SecureStringHelper.Unprotect(password);

            // Convert the SecureString to a plain string
            IntPtr unmanagedString = IntPtr.Zero;
            try {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                password = Marshal.PtrToStringUni(unmanagedString);
            } finally {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
        try {
            Client.Authenticate(username, password);
            Settings.Logger.WriteVerbose($"Send-EmailMessage - Authenticated as {username}");
            return new SmtpResult(true, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
            Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Username? ({username} was used), Password?.");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Authenticate, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
        }
    }

    public SmtpResult Send() {
        try {
            Client.Send(Message);
            Settings.Logger.WriteVerbose($"Send-EmailMessage - Sent email to {SentTo}");
            return new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
            Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Message? ({Message} was used).");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
        }
    }

    public void Disconnect() {
        Client.Disconnect(true);
        stopwatch.Stop();
    }

    public void Dispose() {
        Client.Dispose();
        stopwatch.Stop();
    }

    public SmtpResult Encrypt(string pfxFilePath, string password) {
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
            return new SmtpResult(true, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, "Certificate not found in the store.");
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
                Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
                Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
                if (ErrorAction == ActionPreference.Stop) {
                    throw;
                }
                return new SmtpResult(false, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
            }
        }

        Message = message;
        return new SmtpResult(true, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
    }

    public SmtpResult MultipartSign(X509Certificate2 certificate) {
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
                Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
                Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
                if (ErrorAction == ActionPreference.Stop) {
                    throw;
                }
                return new SmtpResult(false, EmailAction.SMimeSignature, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
            }
        }
        Message = message;
        return new SmtpResult(true, EmailAction.SMimeSignature, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
    }

    public SmtpResult MultipartSign(string pfxFilePath, string password) {
        X509Certificate2 certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return MultipartSign(certificate);
    }

    public SmtpResult MultipartSign(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        store.Close();

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to sign the email
            return MultipartSign(certificates[0]);
        } else {
            throw new Exception("Certificate not found in the store.");
        }
    }

    public void Pkcs7Sign(string pfxFilePath, string password) {
        X509Certificate2 certificate = new X509Certificate2(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        Pkcs7Sign(certificate);
    }

    public void Pkcs7Sign(string certificateThumbprint) {
        // Load the certificate from the Windows Certificate Store
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

        store.Close();

        if (certificates.Count > 0) {
            // Use the certificate directly from the store to sign the email
            Pkcs7Sign(certificates[0]);
        } else {
            throw new Exception("Certificate not found in the store.");
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
            return new SmtpResult(true, EmailAction.SMimeSignaturePKCS7, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
        } catch (Exception ex) {
            Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
            Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Certificate? ({certificate.Thumbprint} was used).");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.SMimeSignaturePKCS7, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
        }
    }

    public SmtpResult SignAndEncrypt(string certificateThumbprint) {
        // Sign the email
        SmtpResult signResult = MultipartSign(certificateThumbprint);
        if (!signResult.Status) {
            return signResult;
        }

        // Encrypt the signed email
        SmtpResult encryptResult = Encrypt(certificateThumbprint);
        if (!encryptResult.Status) {
            return encryptResult;
        }

        return new SmtpResult(true, EmailAction.SMimeSignAndEncrypt, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
    }

    public SmtpResult SignAndEncrypt(string pfxFilePath, string password) {
        // Sign the email
        SmtpResult signResult = MultipartSign(pfxFilePath, password);
        if (!signResult.Status) {
            return signResult;
        }

        // Encrypt the signed email
        SmtpResult encryptResult = Encrypt(pfxFilePath, password);
        if (!encryptResult.Status) {
            return encryptResult;
        }

        return new SmtpResult(true, EmailAction.SMimeSignAndEncrypt, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
    }

    public void EncryptPgp(MimeMessage message) {
        // encrypt our message body using our custom GnuPG cryptography context
        using (var ctx = new MyGnuPGContext()) {
            // Note: this assumes that each of the recipients has a PGP key associated
            // with their email address in the user's public keyring.
            // 
            // If this is not the case, you can use SecureMailboxAddresses instead of
            // normal MailboxAddresses which would allow you to specify the fingerprint
            // of their PGP keys. You could also choose to use one of the Encrypt()
            // overloads that take a list of PgpPublicKeys.
            Message.Body = MultipartEncrypted.Encrypt(ctx, Message.To.Mailboxes, Message.Body);
        }
    }

    public void SignPgp() {
        // digitally sign our message body using our custom GnuPG cryptography context
        using (var ctx = new MyGnuPGContext()) {
            // Note: this assumes that the Sender address has an S/MIME signing certificate
            // and private key with an X.509 Subject Email identifier that matches the
            // sender's email address.
            // 
            // If this is not the case, you can use a SecureMailboxAddress instead of a
            // normal MailboxAddress which would allow you to specify the fingerprint
            // of the sender's private PGP key. You could also choose to use one of the
            // Create() overloads that take a PgpSecretKey, instead.
            var sender = Message.From.Mailboxes.FirstOrDefault();

            Message.Body = MultipartSigned.Create(ctx, sender, DigestAlgorithm.Sha1, Message.Body);
        }
    }

    public void SignPgp(PgpSecretKey key) {
        // digitally sign our message body using our custom GnuPG cryptography context
        using (var ctx = new MyGnuPGContext()) {
            Message.Body = MultipartSigned.Create(ctx, key, DigestAlgorithm.Sha1, Message.Body);
        }
    }


    //public SmtpResult EncryptPgp(string publicKeyPath, string privateKeyPath, string passPhrase) {
    //    try {
    //        byte[] encryptedData;
    //        using (MemoryStream outputStream = new MemoryStream())
    //        using (Stream publicKeyStream = File.OpenRead(publicKeyPath))
    //        using (Stream privateKeyStream = File.OpenRead(privateKeyPath))
    //        using (MemoryStream messageStream = new MemoryStream()) {
    //            Message.Body.WriteTo(messageStream);
    //            messageStream.Position = 0;

    //            EncryptionKeys encryptionKeys = new EncryptionKeys(publicKeyStream, privateKeyStream, passPhrase);
    //            PGP pgp = new PGP(encryptionKeys);
    //            pgp.EncryptStreamAndSign(messageStream, outputStream, false, true);

    //            // Convert the outputStream to a byte array
    //            encryptedData = outputStream.ToArray();
    //        }

    //        // Create a new MemoryStream from the byte array
    //        MemoryStream encryptedStream = new MemoryStream(encryptedData);
    //        // Create a new MimePart with the encrypted data
    //        var encryptedPart = new MimePart("application", "octet-stream") {
    //            Content = new MimeContent(encryptedStream, ContentEncoding.Default),
    //            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
    //            ContentTransferEncoding = ContentEncoding.Base64,
    //            FileName = "encrypted.pgp"
    //        };

    //        // Create a new MimeMessage with the encrypted part as the body
    //        //    var encryptedMessage = new MimeMessage();
    //        //encryptedMessage.Body = encryptedPart;

    //        // Assign the encrypted message back to the Message property
    //        Message.Body = encryptedPart;

    //        return new SmtpResult(true, EmailAction.PgpEncrypt, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
    //    } catch (Exception ex) {
    //        return new SmtpResult(false, EmailAction.PgpEncrypt, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
    //    }
    //}

    //public SmtpResult SignPgp(string publicKeyPath, string privateKeyPath, string passPhrase) {
    //    try {
    //        byte[] signedData;
    //        using (MemoryStream outputStream = new MemoryStream())
    //        using (Stream publicKeyStream = File.OpenRead(publicKeyPath))
    //        using (Stream privateKeyStream = File.OpenRead(privateKeyPath))
    //        using (MemoryStream messageStream = new MemoryStream()) {
    //            Message.Body.WriteTo(messageStream);
    //            messageStream.Position = 0;

    //            EncryptionKeys encryptionKeys = new EncryptionKeys(publicKeyStream, privateKeyStream, passPhrase);
    //            PGP pgp = new PGP(encryptionKeys);
    //            pgp.SignStream(messageStream, outputStream, true);

    //            // Convert the outputStream to a byte array
    //            signedData = outputStream.ToArray();
    //        }

    //        // Create a new MemoryStream from the byte array
    //        MemoryStream signedStream = new MemoryStream(signedData);
    //        // Create a new MimePart with the signed data
    //        var signedPart = new MimePart("application", "octet-stream") {
    //            Content = new MimeContent(signedStream, ContentEncoding.Default),
    //            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
    //            ContentTransferEncoding = ContentEncoding.Base64,
    //            FileName = "signed.pgp"
    //        };

    //        // Create a multipart/mixed container to hold the original message body and the signature
    //        var multipart = new Multipart("mixed");
    //        multipart.Add(Message.Body);
    //        multipart.Add(signedPart);

    //        // Replace the body of the existing MimeMessage with the multipart container
    //        Message.Body = multipart;

    //        return new SmtpResult(true, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, Logging);
    //    } catch (Exception ex) {
    //        return new SmtpResult(false, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, stopwatch.Elapsed, ex.Message);
    //    }
    //}
}