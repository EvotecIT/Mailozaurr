using MimeKit;
using System.Collections.Generic;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Utility methods for working with <see cref="MimeKit"/> objects.
/// </summary>
public static class MimeKitUtils {
    /// <summary>
    /// Saves the provided MIME attachments to the specified directory.
    /// </summary>
    /// <param name="attachments">Collection of MIME entities representing attachments.</param>
    /// <param name="path">Directory path where attachments should be saved.</param>
    public static void SaveAttachments(IEnumerable<MimeEntity> attachments, string path) {
        var resolved = Path.GetFullPath(path);
        if (!Directory.Exists(resolved)) Directory.CreateDirectory(resolved);
        foreach (var attachment in attachments) {
            if (attachment is MimePart mp) {
                var file = Path.Combine(resolved, mp.FileName ?? Path.GetRandomFileName());
                using var fs = File.Create(file);
                mp.Content.DecodeTo(fs);
            } else if (attachment is MessagePart msgPart) {
                var name = msgPart.ContentDisposition?.FileName ?? msgPart.ContentType.Name ?? Path.GetRandomFileName();
                var file = Path.Combine(resolved, name);
                msgPart.Message.WriteTo(file);
            }
        }
    }

    /// <summary>Determines the encryption or signing type of a message.</summary>
    public static EmailEncryption GetEncryption(MimeMessage message) {
        if (message.Body is MultipartEncrypted encrypted) {
            var protocol = encrypted.ContentType.Parameters["protocol"];
            if (!string.IsNullOrEmpty(protocol) && protocol.Equals("application/pgp-encrypted", System.StringComparison.OrdinalIgnoreCase)) {
                return EmailEncryption.PgpEncrypted;
            }
        }

        if (message.Body is ApplicationPkcs7Mime pkcs7 && pkcs7.SecureMimeType == SecureMimeType.EnvelopedData) {
            return EmailEncryption.SmimeEncrypted;
        }

        if (message.Body is MultipartSigned signed) {
            var mimeType = signed[1].ContentType.MimeType;
            if (mimeType.Equals("application/pgp-signature", System.StringComparison.OrdinalIgnoreCase)) {
                return EmailEncryption.PgpSigned;
            }
            if (mimeType.Equals("application/pkcs7-signature", System.StringComparison.OrdinalIgnoreCase) ||
                mimeType.Equals("application/x-pkcs7-signature", System.StringComparison.OrdinalIgnoreCase)) {
                return EmailEncryption.SmimeSigned;
            }
        }

        return EmailEncryption.None;
    }

    /// <summary>Decrypts a PGP encrypted message using the specified private key.</summary>
    public static MimeMessage DecryptPgp(MimeMessage message, string privateKeyPath, string password) {
        if (GetEncryption(message) != EmailEncryption.PgpEncrypted) return message;
        using var ctx = new EphemeralOpenPgpContext(password);
        using (var sec = File.OpenRead(privateKeyPath))
            ctx.Import(new Org.BouncyCastle.Bcpg.OpenPgp.PgpSecretKeyRingBundle(new Org.BouncyCastle.Bcpg.ArmoredInputStream(sec)));
        var encrypted = (MultipartEncrypted)message.Body;
        var decrypted = encrypted.Decrypt(ctx);
        var result = new MimeMessage(message.Headers);
        result.Body = decrypted;
        return result;
    }

    /// <summary>Decrypts an S/MIME encrypted message using the provided certificate.</summary>
    public static MimeMessage DecryptSmime(MimeMessage message, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate) {
        if (GetEncryption(message) != EmailEncryption.SmimeEncrypted) return message;
        using var ctx = new MimeKit.Cryptography.TemporarySecureMimeContext();
        ctx.Import(certificate);
        var pkcs7 = (ApplicationPkcs7Mime)message.Body;
        var decrypted = pkcs7.Decrypt(ctx);
        var result = new MimeMessage(message.Headers);
        result.Body = decrypted;
        return result;
    }

    /// <summary>Verifies a PGP signed message using the given public key.</summary>
    public static bool VerifyPgpSignature(MimeMessage message, string publicKeyPath) {
        if (GetEncryption(message) != EmailEncryption.PgpSigned) return false;
        using var ctx = new EphemeralOpenPgpContext();
        using (var pub = File.OpenRead(publicKeyPath))
            ctx.Import(pub);
        var signed = (MultipartSigned)message.Body;
        var signatures = signed.Verify(ctx);
        foreach (var sig in signatures) sig.Verify();
        return true;
    }

    /// <summary>Verifies an S/MIME signed message.</summary>
    public static bool VerifySmimeSignature(MimeMessage message, params System.Security.Cryptography.X509Certificates.X509Certificate2[] certificates) {
        if (GetEncryption(message) != EmailEncryption.SmimeSigned) return false;
        using var ctx = new MimeKit.Cryptography.TemporarySecureMimeContext();
        foreach (var cert in certificates) ctx.Import(cert);
        var signed = (MultipartSigned)message.Body;
        var signatures = signed.Verify(ctx);
        foreach (var sig in signatures) sig.Verify();
        return true;
    }
}