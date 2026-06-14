using System.Security;
using Xunit;

namespace Mailozaurr.Tests;

public class CredentialHelpersTests {
    [Fact]
    public void ToSecureString_ReturnsEmpty_WhenInputIsNull() {
        SecureString result = Mailozaurr.PowerShell.CredentialHelpers.ToSecureString(null);
        Assert.NotNull(result);
        Assert.Equal(0, result.Length);
    }
}