using Org.BouncyCastle.Bcpg.OpenPgp;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr;

public partial class Smtp {
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
        var body = message.Body;
        if (body is null) {
            const string messageText = "Message body is empty.";
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidOperationException(messageText);
            }
            return new SmtpResult(false, EmailAction.SMimeEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
        }
        // encrypt our message body using a temporary S/MIME context to avoid SQLite dependency
        using (var ctx = new TemporarySecureMimeContext()) {
            try {
                // Create a CmsRecipientCollection and add the CmsRecipient to it
                var recipients = new CmsRecipientCollection();
                recipients.Add(new CmsRecipient(certificate));

                // Encrypt the message body with the certificate
                message.Body = ApplicationPkcs7Mime.Encrypt(ctx, recipients, body!);
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
        var body = message.Body;
        if (body is null) {
            const string messageText = "Message body is empty.";
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidOperationException(messageText);
            }
            return new SmtpResult(false, EmailAction.SMimeSignature, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
        }
        // digitally sign our message body using a temporary S/MIME context
        // TemporarySecureMimeContext avoids the SQLite dependency of DefaultSecureMimeContext
        using (var ctx = new TemporarySecureMimeContext()) {
            try {
                var signer = new CmsSigner(certificate) {
                    DigestAlgorithm = DigestAlgorithm.Sha1
                };
                message.Body = MultipartSigned.Create(ctx, signer, body!);
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
            var body = message.Body;
            if (body is null) {
                const string messageText = "Message body is empty.";
                if (ErrorAction == ActionPreference.Stop) {
                    throw new InvalidOperationException(messageText);
                }
                return new SmtpResult(false, EmailAction.SMimeSignaturePKCS7, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
            }
            // digitally sign our message body using a temporary S/MIME context to avoid SQLite dependency
            using (var ctx = new TemporarySecureMimeContext()) {
                // Create a signer with the certificate
                var signer = new CmsSigner(certificate) {
                    DigestAlgorithm = DigestAlgorithm.Sha256
                };

                // Sign the message body with the signer
                message.Body = ApplicationPkcs7Mime.Sign(ctx, signer, body!);
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
        var body = message.Body;
        if (body is null) {
            const string messageText = "Message body is empty.";
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidOperationException(messageText);
            }
            return new SmtpResult(false, EmailAction.PgpEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
        }
        using (var ctx = new EphemeralOpenPgpContext()) {
            using (var pub = File.OpenRead(publicKeyPath))
                ctx.Import(pub);
            var recipients = message.To.Mailboxes.Concat(message.Cc.Mailboxes).Concat(message.Bcc.Mailboxes).ToList();
            try {
                var keys = ctx.GetPublicKeys(recipients);
                message.Body = MultipartEncrypted.Encrypt(ctx, keys, body!);
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
            var body = message.Body;
            if (body is null) {
                const string messageText = "Message body is empty.";
                if (ErrorAction == ActionPreference.Stop) {
                    throw new InvalidOperationException(messageText);
                }
                return new SmtpResult(false, EmailAction.PgpSign, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
            }
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
                    message.Body = MultipartSigned.Create(ctx, signingKey, DigestAlgorithm.Sha256, body!);
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
            var body = message.Body;
            if (body is null) {
                const string messageText = "Message body is empty.";
                if (ErrorAction == ActionPreference.Stop) {
                    throw new InvalidOperationException(messageText);
                }
                return new SmtpResult(false, EmailAction.PgpSignAndEncrypt, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText);
            }
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
                    message.Body = MultipartEncrypted.SignAndEncrypt(ctx, signingKey, DigestAlgorithm.Sha256, EncryptionAlgorithm.Cast5, encKeys, body!);
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