namespace Mailozaurr;

/// <summary>
/// Specifies encryption or signing actions for S/MIME operations.
/// </summary>
public enum EmailActionEncryption {
    None,
    SMIMESign,
    SMIMESignPkcs7,
    SMIMEEncrypt,
    SMIMESignAndEncrypt,
}

