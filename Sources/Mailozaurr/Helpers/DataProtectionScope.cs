#if !UNIX
namespace Mailozaurr;

internal enum DataProtectionScope {
    CurrentUser = 0x00,
    LocalMachine = 0x01
}
#endif
