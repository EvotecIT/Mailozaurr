namespace Mailozaurr;

public enum EmailAction {
    Authenticate,
    Connect,
    SMimeSignature,
    SMimeSignaturePKCS7,
    SMimeEncrypt,
    SMimeSignAndEncrypt,
    PgpSign,
    PgpEncrypt,
    PgpSignAndEncrypt,
    Send
}