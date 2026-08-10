using OfficeIMO.Email;
using OfficeIMO.Security;
using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr;

internal static class MailFileSignatureProjection {
    internal static MailFileSignatureInfo Evaluate(EmailDocument document, EmailReaderOptions contentReaderOptions) {
        EmailSmimeVerificationResult verification = EmailSmime.Verify(
            document,
            OfficeSecurityProvider.Default,
            contentReaderOptions: contentReaderOptions);
        var signer = verification.Cryptography?.Signers.FirstOrDefault();
        return signer == null
            ? new MailFileSignatureInfo(verification, null, null, null)
            : new MailFileSignatureInfo(
                verification,
                verification.IsCryptographicallyValid,
                GetCompatibilitySignerName(signer.SignerCertificate, signer.Subject),
                signer.SigningTime);
    }

    private static string? GetCompatibilitySignerName(byte[]? certificateBytes, string? subject) {
        if (certificateBytes != null && certificateBytes.Length > 0) {
#pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(certificateBytes);
#pragma warning restore SYSLIB0057
            string name = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            if (!string.IsNullOrWhiteSpace(name)) return name;
            string email = certificate.GetNameInfo(X509NameType.EmailName, forIssuer: false);
            if (!string.IsNullOrWhiteSpace(email)) return email;
        }

        return subject;
    }
}

internal readonly struct MailFileSignatureInfo {
    internal MailFileSignatureInfo(EmailSmimeVerificationResult verification, bool? isValid,
        string? signedBy, DateTimeOffset? signedOn) {
        Verification = verification;
        IsValid = isValid;
        SignedBy = signedBy;
        SignedOn = signedOn;
    }

    internal EmailSmimeVerificationResult? Verification { get; }

    internal bool? IsValid { get; }

    internal string? SignedBy { get; }

    internal DateTimeOffset? SignedOn { get; }
}
