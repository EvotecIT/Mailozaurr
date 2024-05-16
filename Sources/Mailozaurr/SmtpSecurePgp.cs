using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;

namespace Mailozaurr;

public class MyGnuPGContext : GnuPGContext {
    public MyGnuPGContext() {
    }

    protected override string GetPasswordForKey(PgpSecretKey key) {
        // prompt the user (or a secure password cache) for the password for the specified secret key.
        return "password";
    }
}