namespace Mailozaurr;

/// <summary>
/// Represents actions performed when sending or preparing an email.
/// </summary>
public enum EmailAction {
    Authenticate,
    Connect,
    SMimeSignature,
    SMimeSignaturePKCS7,
    SMimeEncrypt,
    SMimeSignAndEncrypt,
    PgpSign,
    PgpEncrypt,
    Send,
    SendDraftMessage,
    SendAttachment
}
