using MailKit.Security;

namespace Mailozaurr;

internal static class SmtpSecurityModeExtensions {
    internal static SecureSocketOptions ToMailKit(this SmtpSecurityMode securityMode) {
        return securityMode switch {
            SmtpSecurityMode.Auto => SecureSocketOptions.Auto,
            SmtpSecurityMode.None => SecureSocketOptions.None,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
            _ => throw new ArgumentOutOfRangeException(nameof(securityMode), securityMode, "Unsupported SMTP security mode.")
        };
    }

    internal static SmtpSecurityMode ToSmtpSecurityMode(this SecureSocketOptions secureSocketOptions) {
        return secureSocketOptions switch {
            SecureSocketOptions.Auto => SmtpSecurityMode.Auto,
            SecureSocketOptions.None => SmtpSecurityMode.None,
            SecureSocketOptions.SslOnConnect => SmtpSecurityMode.SslOnConnect,
            SecureSocketOptions.StartTls => SmtpSecurityMode.StartTls,
            SecureSocketOptions.StartTlsWhenAvailable => SmtpSecurityMode.StartTlsWhenAvailable,
            _ => throw new ArgumentOutOfRangeException(nameof(secureSocketOptions), secureSocketOptions, "Unsupported MailKit secure socket option.")
        };
    }

    internal static SecureSocketOptions ResolveEffective(this SecureSocketOptions secureSocketOptions, bool useSsl) {
        return useSsl && secureSocketOptions == SecureSocketOptions.Auto
            ? SecureSocketOptions.StartTls
            : secureSocketOptions;
    }
}
