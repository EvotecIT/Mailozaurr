namespace Mailozaurr;

/// <summary>
/// Represents actions performed when sending or preparing an email.
/// </summary>
/// <remarks>
/// These values are used internally when tracing the steps of
/// the email sending pipeline.
/// </remarks>
public enum EmailAction {
    /// <summary>
    /// Authenticate with the mail server or provider.
    /// </summary>
    Authenticate,

    /// <summary>
    /// Establish a connection to the mail server.
    /// </summary>
    Connect,

    /// <summary>
    /// Sign the message using S/MIME.
    /// </summary>
    SMimeSignature,

    /// <summary>
    /// Sign the message using S/MIME in PKCS#7 format.
    /// </summary>
    SMimeSignaturePKCS7,

    /// <summary>
    /// Encrypt the message using S/MIME.
    /// </summary>
    SMimeEncrypt,

    /// <summary>
    /// Sign and encrypt the message using S/MIME.
    /// </summary>
    SMimeSignAndEncrypt,

    /// <summary>
    /// Sign the message using PGP.
    /// </summary>
    PgpSign,

    /// <summary>
    /// Encrypt the message using PGP.
    /// </summary>
    PgpEncrypt,

    /// <summary>
    /// Sign and encrypt the message using PGP.
    /// </summary>
    PgpSignAndEncrypt,

    /// <summary>
    /// Send the message via the configured provider.
    /// </summary>
    Send,

    /// <summary>
    /// Send the message as a draft.
    /// </summary>
    SendDraftMessage,

    /// <summary>
    /// Send message attachments only.
    /// </summary>
    SendAttachment
}