namespace Mailozaurr;

/// <summary>
/// Supported SASL authentication mechanisms.
/// </summary>
/// <remarks>
/// Values correspond to mechanisms understood by <c>MailKit</c>
/// and can be used when configuring SMTP connections.
/// </remarks>
public enum AuthenticationMechanism {
    /// <summary>
    /// Plain text authentication mechanism.
    /// </summary>
    Plain = 0,

    /// <summary>
    /// LOGIN authentication mechanism.
    /// </summary>
    Login = 1,

    /// <summary>
    /// Challenge-response authentication using CRAM-MD5.
    /// </summary>
    CramMd5 = 2,

    /// <summary>
    /// Automatically selects a mechanism advertised by the SMTP server.
    /// </summary>
    Auto = 3
}
