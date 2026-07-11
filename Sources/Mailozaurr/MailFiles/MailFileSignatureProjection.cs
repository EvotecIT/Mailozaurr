using MimeKit;
using MimeKit.Cryptography;
using OfficeIMO.Email;

namespace Mailozaurr;

internal static class MailFileSignatureProjection {
    internal static MailFileSignatureInfo Evaluate(EmailDocument document, MimeMessage? mimeMessage) {
        if (mimeMessage?.Body != null) return Evaluate(mimeMessage.Body);
        if (!MailFileMimeAdapter.TryGetProtectedMimeEntity(document, out MimeEntity? entity) || entity == null) {
            return default;
        }

        using (entity) {
            return Evaluate(entity);
        }
    }

    private static MailFileSignatureInfo Evaluate(MimeEntity entity) {
        try {
            DigitalSignatureCollection? signatures = GetSignatures(entity);
            if (signatures == null || signatures.Count == 0) return default;

            bool valid = true;
            foreach (IDigitalSignature signature in signatures) {
                try {
                    valid &= signature.Verify(true);
                } catch (DigitalSignatureVerifyException) {
                    valid = false;
                }
            }

            IDigitalSignature first = signatures[0];
            IDigitalCertificate? certificate = first.SignerCertificate;
            string? signedBy = FirstNonEmpty(certificate?.Name, certificate?.Email);
            return new MailFileSignatureInfo(valid, signedBy, first.CreationDate);
        } catch (FormatException) {
            return default;
        } catch (InvalidOperationException) {
            return default;
        } catch (NotSupportedException) {
            return default;
        } catch (Org.BouncyCastle.Cms.CmsException) {
            return default;
        }
    }

    private static DigitalSignatureCollection? GetSignatures(MimeEntity entity) {
        using var context = new TemporarySecureMimeContext();
        if (entity is MultipartSigned multipart && IsSmimeSignature(multipart)) {
            return multipart.Verify(context);
        }
        if (entity is ApplicationPkcs7Mime pkcs7 && pkcs7.SecureMimeType == SecureMimeType.SignedData) {
            DigitalSignatureCollection signatures = pkcs7.Verify(context, out MimeEntity extracted);
            extracted.Dispose();
            return signatures;
        }
        return null;
    }

    private static bool IsSmimeSignature(MultipartSigned multipart) {
        string? protocol = multipart.ContentType.Parameters["protocol"];
        return string.Equals(protocol, "application/pkcs7-signature", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(protocol, "application/x-pkcs7-signature", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

internal readonly struct MailFileSignatureInfo {
    internal MailFileSignatureInfo(bool isValid, string? signedBy, DateTimeOffset signedOn) {
        IsValid = isValid;
        SignedBy = signedBy;
        SignedOn = signedOn;
    }

    internal bool? IsValid { get; }

    internal string? SignedBy { get; }

    internal DateTimeOffset? SignedOn { get; }
}
