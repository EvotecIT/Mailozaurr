namespace Mailozaurr;

/// <summary>
/// Specifies how an SMTP connection establishes transport security without exposing the underlying provider type.
/// </summary>
public enum SmtpSecurityMode {
    /// <summary>Uses an unencrypted SMTP connection.</summary>
    None = 0,

    /// <summary>Lets Mailozaurr select the transport security from the server and port.</summary>
    Auto = 1,

    /// <summary>Establishes TLS immediately when the connection is opened.</summary>
    SslOnConnect = 2,

    /// <summary>Requires the server to upgrade the connection with STARTTLS.</summary>
    StartTls = 3,

    /// <summary>Uses STARTTLS when the server advertises it and otherwise continues without TLS.</summary>
    StartTlsWhenAvailable = 4
}
