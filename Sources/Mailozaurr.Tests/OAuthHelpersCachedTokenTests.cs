using System;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class OAuthHelpersCachedTokenTests {
    [Fact]
    public async Task AcquireO365TokenCachedAsync_ReturnsCachedToken() {
        var cacheKey = "o365:test@example.com";
        var credential = new OAuthCredential {
            UserName = "test@example.com",
            AccessToken = "token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        await OAuthTokenCache.SetAsync(cacheKey, credential);

        var result = await OAuthHelpers.AcquireO365TokenCachedAsync(
            "test@example.com",
            "client-id",
            "tenant-id",
            "https://login.microsoftonline.com/common/oauth2/nativeclient",
            Array.Empty<string>());

        Assert.Equal(credential.AccessToken, result.AccessToken);
        Assert.Equal(credential.UserName, result.UserName);
    }
}

