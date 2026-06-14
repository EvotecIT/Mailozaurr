using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class SecureStringHelperAsyncTests {
    private static string ToPlainString(SecureString s) {
        IntPtr ptr = Marshal.SecureStringToCoTaskMemUnicode(s);
        try {
            return Marshal.PtrToStringUni(ptr)!;
        } finally {
            Marshal.ZeroFreeCoTaskMemUnicode(ptr);
        }
    }

    [Fact]
    public async Task EncryptDecryptAsync_RoundTrip() {
        using SecureString input = SecureStringHelper.FromPlainTextString("secret");
        byte[] key = new byte[32];
        new Random().NextBytes(key);
        var result = await SecureStringHelper.EncryptAsync(input, key);
        using SecureString decrypted = await SecureStringHelper.DecryptAsync(result.EncryptedData, key, Convert.FromBase64String(result.IV));
        Assert.Equal("secret", ToPlainString(decrypted));
    }
}