namespace Mailozaurr;

/// <summary>
/// Indicates the encryption or signing format of an email message.
/// </summary>
/// <remarks>
/// Determined using MIME headers and helps identify whether a
/// message has been secured.
/// </remarks>
public enum EmailEncryption {
    /// <summary>No encryption or signature detected.</summary>
    None,
    /// <summary>The message is encrypted with OpenPGP.</summary>
    PgpEncrypted,
    /// <summary>The message is signed using OpenPGP.</summary>
    PgpSigned,
    /// <summary>The message is encrypted using S/MIME.</summary>
    SmimeEncrypted,
    /// <summary>The message is signed using S/MIME.</summary>
    SmimeSigned
}