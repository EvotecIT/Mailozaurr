namespace Mailozaurr;

public enum EmailActionEncryption {
    None,
    SMIMESign,
    SMIMESignPkcs7,
    SMIMEEncrypt,
    SMIMESignAndEncrypt,
}
