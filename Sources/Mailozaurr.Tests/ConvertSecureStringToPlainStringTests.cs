using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class ConvertSecureStringToPlainStringTests
{
    [Fact]
    public void ReturnsPlainPassword_WhenNotSecure()
    {
        var smtp = new Smtp();
        var result = smtp.ConvertSecureStringToPlainString("secret", false);
        Assert.Equal("secret", result);
    }

    [Fact]
    public void ReturnsDecryptedPassword_WhenSecure()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)) return;
        var smtp = new Smtp();
        byte[] bytes = Encoding.Unicode.GetBytes("secret");
        string protectedStr = string.Concat(bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

        var result = smtp.ConvertSecureStringToPlainString(protectedStr, true);

        Assert.Equal("secret", result);
    }
}
