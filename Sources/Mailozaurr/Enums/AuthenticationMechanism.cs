namespace Mailozaurr;

/// <summary>
/// Supported SASL authentication mechanisms.
/// </summary>
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
