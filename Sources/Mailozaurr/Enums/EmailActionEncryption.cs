namespace Mailozaurr;

/// <summary>
/// Specifies encryption or signing actions for S/MIME operations.
/// </summary>
/// <remarks>
/// The order of the enum values mirrors the processing flow when
/// applying signatures or encryption to a message.
/// </remarks>
public enum EmailActionEncryption {
    /// <summary>
    /// No signing or encryption is applied.
    /// </summary>
    None,

    /// <summary>
    /// Sign the message using an S/MIME certificate.
    /// </summary>
    SMIMESign,

    /// <summary>
    /// Sign the message using an S/MIME certificate in PKCS#7 format.
    /// </summary>
    SMIMESignPkcs7,

    /// <summary>
    /// Encrypt the message using S/MIME.
    /// </summary>
    SMIMEEncrypt,

    /// <summary>
    /// Sign and encrypt the message using S/MIME.
    /// </summary>
    SMIMESignAndEncrypt,

    /// <summary>
    /// Sign the message using a PGP key.
    /// </summary>
    PGPSign,

    /// <summary>
    /// Encrypt the message using a PGP key.
    /// </summary>
    PGPEncrypt,

    /// <summary>
    /// Sign and encrypt the message using a PGP key.
    /// </summary>
    PGPSignAndEncrypt,
}

