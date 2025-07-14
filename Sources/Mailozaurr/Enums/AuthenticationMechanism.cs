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
    Plain,

    /// <summary>
    /// LOGIN authentication mechanism.
    /// </summary>
    Login,

    /// <summary>
    /// Challenge-response authentication using CRAM-MD5.
    /// </summary>
    CramMd5
}
