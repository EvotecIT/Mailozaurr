using System;
using System.Text;

namespace Mailozaurr.Tests.Cryptography;

public sealed class CredentialProtectionTests {
    [Fact]
    public void ProtectRoundTripsSecret() {
        const string secret = "P@ssw0rd!";

        var protector = CredentialProtection.Default;
        var encrypted = protector.Protect(secret);
        var decrypted = protector.Unprotect(encrypted);

        Assert.Equal(secret, decrypted);
    }

    [Fact]
    public void ProtectDoesNotReturnPlainBase64() {
        const string secret = "AnotherSecret";

        var protector = CredentialProtection.Default;
        var encrypted = protector.Protect(secret);
        var plainBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));

        Assert.NotEqual(plainBase64, encrypted);
    }
}
