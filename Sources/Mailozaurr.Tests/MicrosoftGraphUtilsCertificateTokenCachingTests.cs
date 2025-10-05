using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Mailozaurr;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class MicrosoftGraphUtilsCertificateTokenCachingTests : IDisposable {
    public MicrosoftGraphUtilsCertificateTokenCachingTests() {
        ClearCaches();
    }

    [Fact]
    public async Task ConnectO365GraphAsync_WithCertificateBytes_UsesCachedToken() {
        ClearCaches();
        int callCount = 0;
        var authorization = new GraphAuthorization {
            AccessToken = "bytes-token",
            TokenType = "Bearer",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        MicrosoftGraphUtils.AcquireGraphCertificateBytesTokenAsyncFunc = (clientId, tenant, bytes, password, scopes) => {
            callCount++;
            return Task.FromResult(authorization);
        };
        var credential = new GraphCredential {
            ClientId = "client-id",
            DirectoryId = "tenant-id",
            CertificateBytes = new byte[] { 1, 2, 3 },
            CertificatePassword = "pwd"
        };

        try {
            var token1 = await MicrosoftGraphUtils.ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            var token2 = await MicrosoftGraphUtils.ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");

            Assert.Equal("Bearer bytes-token", token1);
            Assert.Equal(token1, token2);
            Assert.Equal(1, callCount);
        } finally {
            MicrosoftGraphUtils.ResetOAuthHelperOverrides();
        }
    }

    [Fact]
    public async Task ConnectO365GraphAsync_WithCertificatePem_UsesCachedToken() {
        ClearCaches();
        int callCount = 0;
        var authorization = new GraphAuthorization {
            AccessToken = "pem-token",
            TokenType = "Bearer",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        MicrosoftGraphUtils.AcquireGraphCertificatePemTokenAsyncFunc = (clientId, tenant, path, scopes) => {
            callCount++;
            return Task.FromResult(authorization);
        };
        var credential = new GraphCredential {
            ClientId = "client-id",
            DirectoryId = "tenant-id",
            CertificatePemPath = "certificate.pem"
        };

        try {
            var token1 = await MicrosoftGraphUtils.ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            var token2 = await MicrosoftGraphUtils.ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");

            Assert.Equal("Bearer pem-token", token1);
            Assert.Equal(token1, token2);
            Assert.Equal(1, callCount);
        } finally {
            MicrosoftGraphUtils.ResetOAuthHelperOverrides();
        }
    }

    private static void ClearCaches() {
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static);
        if (cacheField?.GetValue(null) is ConcurrentDictionary<string, GraphAuthorization> cache) {
            cache.Clear();
        }

        var oauthType = typeof(MicrosoftGraphUtils).Assembly.GetType("Mailozaurr.OAuthTokenCache");
        var internalCacheField = oauthType?.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        internalCacheField?.SetValue(null, null);
        var cachePathField = oauthType?.GetField("CacheFilePath", BindingFlags.NonPublic | BindingFlags.Static);
        var cachePath = cachePathField?.GetValue(null) as string;
        if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath)) {
            File.Delete(cachePath);
        }
    }

    public void Dispose() {
        MicrosoftGraphUtils.ResetOAuthHelperOverrides();
        ClearCaches();
    }
}
