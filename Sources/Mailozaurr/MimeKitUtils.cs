using Mailozaurr.NonDeliveryReports;
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
                if (mp.Content != null) {
                    mp.Content.DecodeTo(fs);
                } else {
                    mp.WriteTo(fs);
                }
            } else if (attachment is MessagePart msgPart) {
                var name = msgPart.ContentDisposition?.FileName ?? msgPart.ContentType.Name ?? Path.GetRandomFileName();
                var file = Path.Combine(resolved, name);
                if (msgPart.Message != null) {
                    msgPart.Message.WriteTo(file);
                } else {
                    msgPart.WriteTo(file);
                }
            }
        }
    }

    /// <summary>
    /// Attempts to extract all <see cref="NonDeliveryReport"/> instances from a message.
    /// </summary>
    public static IList<NonDeliveryReport> GetNonDeliveryReports(MimeMessage message) {
        var reports = new List<NonDeliveryReport>();
        if (message == null) {
            return reports;
        }

        var status = FindDeliveryStatus(message.Body);
        if (status == null && !SubjectIndicatesNdr(message.Subject)) {
            return reports;
        }

        if (status != null) {
            foreach (var group in status.StatusGroups) {
                var headers = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var header in group) {
                    headers.TryAdd(header.Field, header.Value);
                }

                var report = headers.Count > 0 ? NonDeliveryReport.FromHeaders(headers) : new NonDeliveryReport();
                if (report.Timestamp == System.DateTimeOffset.MinValue) {
                    report.Timestamp = message.Date;
                }
                reports.Add(report);
            }
        } else {
            reports.Add(new NonDeliveryReport { Timestamp = message.Date });
        }

        return reports;
    }

    /// <summary>Attempts to extract the first <see cref="NonDeliveryReport"/> from a message.</summary>
    public static NonDeliveryReport? GetNonDeliveryReport(MimeMessage message) {
        var reports = GetNonDeliveryReports(message);
        return reports.Count > 0 ? reports[0] : null;
    }

    private static MessageDeliveryStatus? FindDeliveryStatus(MimeEntity? entity) {
        if (entity == null) {
            return null;
        }

        if (entity is MessageDeliveryStatus mds) {
            return mds;
        }
        if (entity is Multipart multipart) {
            foreach (var part in multipart) {
                var found = FindDeliveryStatus(part);
                if (found != null) {
                    return found;
                }
            }
        }
        return null;
    }

    private static bool SubjectIndicatesNdr(string? subject) {
        if (string.IsNullOrWhiteSpace(subject)) {
            return false;
        }
        foreach (var p in NonDeliveryReportSubjectPatterns.Values) {
            if (subject!.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
        }
        return false;
    }

    /// <summary>Determines the encryption or signing type of a message.</summary>
    public static EmailEncryption GetEncryption(MimeMessage message) {
        if (message.Body is MultipartEncrypted encrypted) {
            var protocol = encrypted.ContentType.Parameters["protocol"];
            if (!string.IsNullOrEmpty(protocol) && protocol!.Equals("application/pgp-encrypted", System.StringComparison.OrdinalIgnoreCase)) {
                return EmailEncryption.PgpEncrypted;
            }
        }

        if (message.Body is ApplicationPkcs7Mime pkcs7 && pkcs7.SecureMimeType == SecureMimeType.EnvelopedData) {
            return EmailEncryption.SmimeEncrypted;
        }

        if (message.Body is MultipartSigned signed) {
            var mimeType = signed.Count > 1 ? signed[1].ContentType?.MimeType : null;
            if (string.Equals(mimeType, "application/pgp-signature", System.StringComparison.OrdinalIgnoreCase)) {
                return EmailEncryption.PgpSigned;
            }
            if (string.Equals(mimeType, "application/pkcs7-signature", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mimeType, "application/x-pkcs7-signature", System.StringComparison.OrdinalIgnoreCase)) {
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
        if (message.Body is not MultipartEncrypted encrypted) {
            return message;
        }

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
        if (message.Body is not ApplicationPkcs7Mime pkcs7) {
            return message;
        }

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
        if (message.Body is not MultipartSigned signed) {
            return false;
        }

        var signatures = signed.Verify(ctx);
        foreach (var sig in signatures) sig.Verify();
        return true;
    }

    /// <summary>Verifies an S/MIME signed message.</summary>
    public static bool VerifySmimeSignature(MimeMessage message, params System.Security.Cryptography.X509Certificates.X509Certificate2[] certificates) {
        if (GetEncryption(message) != EmailEncryption.SmimeSigned) return false;
        using var ctx = new MimeKit.Cryptography.TemporarySecureMimeContext();
        foreach (var cert in certificates) ctx.Import(cert);
        if (message.Body is not MultipartSigned signed) {
            return false;
        }

        var signatures = signed.Verify(ctx);
        foreach (var sig in signatures) sig.Verify(true);
        return true;
    }
}
