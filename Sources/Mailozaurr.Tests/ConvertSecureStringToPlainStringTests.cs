using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var smtp = new Smtp();

        static string ProtectString(string value)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(value);
            byte[] protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var sb = new StringBuilder(protectedBytes.Length * 2);
            foreach (byte b in protectedBytes)
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        string protectedStr = ProtectString("secret");

        var result = smtp.ConvertSecureStringToPlainString(protectedStr, true);

        Assert.Equal("secret", result);
    }
}
