using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Mailozaurr.Tests;

public class GraphAuthenticationClientTests {
    private const string ClientId = "16fa6cf1-0230-46dd-934b-4a5042cd3355";
    private const string TenantId = "9eb24e6c-1b05-43ef-9751-f50c83548dfa";
    private const string SyntheticSecret = "synthetic-only-secret+&=never-log";

    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact(Skip = "A non-exportable ephemeral CNG key requires Windows.", SkipUnless = nameof(IsWindows))]
    public async Task GetAuthorizationAsync_BorrowedNonExportableCertificate_SignsAssertionAndSurvivesDisposal() {
        using X509Certificate2 certificate = CreateNonExportableCertificate();
        AssertNonExportable(certificate);
        var transport = new TokenTransport();
        GraphCredential credential = CreateCredential();
        credential.Certificate = certificate;
        // Invalid bytes also prove that lower-precedence credentials are not loaded.
        credential.CertificateBytes = new byte[] { 1, 2, 3 };
        using (var client = new GraphAuthenticationClient(credential, send: transport.SendAsync)) {
            GraphAuthorization authorization = await client.GetAuthorizationAsync();
            Assert.Equal("token-1", authorization.AccessToken);
        }

        Dictionary<string, string> form = Assert.Single(transport.Forms);
        Assert.False(form.ContainsKey("client_secret"));
        Assert.Equal(ClientId, form["client_id"]);
        Assert.Equal("client_credentials", form["grant_type"]);
        Assert.Equal("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", form["client_assertion_type"]);
        AssertAssertionSignature(form["client_assertion"], certificate, transport.TokenEndpoint);

        // A successful request alone would miss the client disposing its borrowed certificate.
        AssertNonExportable(certificate);
        AssertCanSign(certificate);
    }

    [Fact]
    public async Task GetAuthorizationAsync_ValidToken_ReusesMsalCacheAndPreservesExpiry() {
        var transport = new TokenTransport();
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);
        DateTimeOffset before = DateTimeOffset.UtcNow;
        GraphAuthorization first = await client.GetAuthorizationAsync();
        DateTimeOffset after = DateTimeOffset.UtcNow;
        GraphAuthorization second = await client.GetAuthorizationAsync();

        Assert.Equal("token-1", first.AccessToken);
        Assert.Equal("Bearer", first.TokenType);
        Assert.InRange(first.ExpiresOn, before.AddSeconds(3595), after.AddSeconds(3605));
        Assert.Equal(first.AccessToken, second.AccessToken);
        Assert.Equal(first.ExpiresOn, second.ExpiresOn);
        Dictionary<string, string> form = Assert.Single(transport.Forms);
        Assert.Equal(SyntheticSecret, form["client_secret"]);
        Assert.Contains("https://graph.microsoft.com/.default", form["scope"].Split(' '));
    }

    [Fact]
    public async Task GetAuthorizationAsync_CustomAuthorityAndScopes_UsesConfiguredTokenEndpoint() {
        var authority = new Uri("https://login.microsoftonline.us/");
        var transport = new TokenTransport(authority);
        const string scope = "https://graph.microsoft.us/.default";
        using var client = new GraphAuthenticationClient(CreateCredential(), authority, transport.SendAsync, new[] { scope });

        GraphAuthorization authorization = await client.GetAuthorizationAsync();

        Assert.Equal("token-1", authorization.AccessToken);
        Assert.Equal(transport.TokenEndpoint, Assert.Single(transport.TokenUris));
        Dictionary<string, string> form = Assert.Single(transport.Forms);
        Assert.Contains(scope, form["scope"].Split(' '));
        Assert.DoesNotContain("https://graph.microsoft.com/.default", form["scope"].Split(' '));
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(60)]
    public async Task GetAuthorizationAsync_ExpiredOrShortLivedToken_RefreshesInsteadOfReusing(int lifetimeSeconds) {
        var transport = new TokenTransport {
            Respond = (call, _) => Task.FromResult(TokenTransport.TokenResponse("token-" + call, call == 1 ? lifetimeSeconds : 3600))
        };
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);

        GraphAuthorization first = await client.GetAuthorizationAsync();
        GraphAuthorization second = await client.GetAuthorizationAsync();
        GraphAuthorization third = await client.GetAuthorizationAsync();

        Assert.Equal("token-1", first.AccessToken);
        Assert.Equal("token-2", second.AccessToken);
        Assert.Equal(second.AccessToken, third.AccessToken);
        Assert.Equal(second.ExpiresOn, third.ExpiresOn);
        Assert.Equal(2, transport.Forms.Count);
    }

    [Fact]
    public async Task GetAuthorizationAsync_ConcurrentCallers_ShareOneTokenAcquisition() {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new TokenTransport {
            Respond = async (_, cancellationToken) => {
                entered.TrySetResult(true);
                using (cancellationToken.Register(() => release.TrySetCanceled())) {
                    await release.Task;
                }
                return TokenTransport.TokenResponse("shared-token", 3600);
            }
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);
        Task<GraphAuthorization> first = client.GetAuthorizationAsync(timeout.Token);
        Task[] startedOrFailed = { entered.Task, first };
        await await Task.WhenAny(startedOrFailed);
        Task<GraphAuthorization>[] followers = Enumerable.Range(0, 7)
            .Select(_ => client.GetAuthorizationAsync(timeout.Token)).ToArray();
        release.TrySetResult(true);
        GraphAuthorization[] authorizations = await Task.WhenAll(new[] { first }.Concat(followers));

        Assert.All(authorizations, authorization => Assert.Equal("shared-token", authorization.AccessToken));
        Assert.All(authorizations, authorization => Assert.Equal(authorizations[0].ExpiresOn, authorization.ExpiresOn));
        Assert.Single(transport.Forms);
    }

    [Fact]
    public async Task GetAuthorizationAsync_CancellationDuringSend_CancelsTransportAndAllowsRetry() {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        bool transportObservedCancellation = false;
        var transport = new TokenTransport {
            Respond = async (call, token) => {
                if (call != 1) return TokenTransport.TokenResponse("retry-token", 3600);
                using (token.Register(() => transportObservedCancellation = true)) {
                    cancellation.Cancel();
                    await Task.Delay(Timeout.Infinite, token);
                }
                throw new InvalidOperationException("Canceled delay unexpectedly completed.");
            }
        };
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);

        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAuthorizationAsync(cancellation.Token));

        Assert.True(transportObservedCancellation);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(SyntheticSecret, error.ToString());
        GraphAuthorization retry = await client.GetAuthorizationAsync();
        Assert.Equal("retry-token", retry.AccessToken);
        Assert.Equal(2, transport.Forms.Count);
    }

    [Fact]
    public async Task GetAuthorizationAsync_PreCanceled_DoesNotSendHttpRequest() {
        var transport = new TokenTransport();
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAuthorizationAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(0, transport.RequestCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAuthorizationAsync_ProviderFailure_DoesNotExposeBodySecretOrInnerException(bool malformedSuccess) {
        const string providerMarker = "private-provider-body-marker";
        var transport = new TokenTransport {
            Respond = (_, _) => Task.FromResult(new HttpResponseMessage(malformedSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest) {
                Content = new StringContent(malformedSuccess
                    ? providerMarker + SyntheticSecret
                    : JsonSerializer.Serialize(new { error = "invalid_client", error_description = providerMarker + SyntheticSecret }),
                    Encoding.UTF8, "application/json")
            })
        };
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAuthorizationAsync());

        Assert.Equal(MicrosoftGraphUtils.AuthenticationFailure, error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(providerMarker, error.ToString());
        Assert.DoesNotContain(SyntheticSecret, error.ToString());
        Assert.Single(transport.Forms);
    }

    [Fact]
    public async Task GetAuthorizationAsync_InjectedPipelineExhaustsServerFailure_DoesNotRetryOutsidePipeline() {
        var transport = new TokenTransport {
            Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
                Content = new StringContent("{\"error\":\"temporarily_unavailable\",\"error_description\":\"synthetic-private-server-failure\"}", Encoding.UTF8, "application/json")
            })
        };
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAuthorizationAsync());

        Assert.Single(transport.Forms);
        Assert.Equal(MicrosoftGraphUtils.AuthenticationFailure, error.Message);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task GetAuthorizationAsync_TransportException_DoesNotExposeSecretOrInnerException() {
        const string providerMarker = "private-transport-marker";
        var transport = new TokenTransport {
            Respond = (_, _) => throw new HttpRequestException(providerMarker + SyntheticSecret,
                new InvalidOperationException(SyntheticSecret))
        };
        using var client = new GraphAuthenticationClient(CreateCredential(), send: transport.SendAsync);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAuthorizationAsync());

        Assert.Equal(MicrosoftGraphUtils.AuthenticationFailure, error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(providerMarker, error.ToString());
        Assert.DoesNotContain(SyntheticSecret, error.ToString());
        Assert.NotEmpty(transport.Forms);
    }

    [Fact(Skip = "A non-exportable ephemeral CNG key requires Windows.", SkipUnless = nameof(IsWindows))]
    public void BuildGraphTokenCacheKey_DistinctCertificates_IsolatesTokensWithoutExportingKeys() {
        using X509Certificate2 first = CreateNonExportableCertificate();
        using X509Certificate2 second = CreateNonExportableCertificate();
        using var firstCopy = new X509Certificate2(first);
        GraphCredential credential = CreateCredential();
        credential.Certificate = first;
        string firstKey = MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, TenantId, "https://graph.microsoft.com");
        credential.Certificate = second;
        string secondKey = MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, TenantId, "https://graph.microsoft.com");
        credential.Certificate = firstCopy;

        Assert.NotEqual(firstKey, secondKey);
        Assert.Equal(firstKey, MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, TenantId, "https://graph.microsoft.com"));
        Assert.DoesNotContain(SyntheticSecret, firstKey);
        AssertNonExportable(first);
        AssertNonExportable(second);
    }

    [Fact]
    public void BuildGraphTokenCacheKey_DifferentStoreSelectors_IsolatesTokensWithoutOpeningStores() {
        GraphCredential credential = CreateCredential();
        credential.CertificateThumbprint = "synthetic-thumbprint-one";
        string baseline = MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, TenantId, "https://graph.microsoft.com");
        credential.CertificateThumbprint = "synthetic-thumbprint-two";
        Assert.NotEqual(baseline, MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, TenantId, "https://graph.microsoft.com"));
        credential.CertificateThumbprint = "synthetic-thumbprint-one";
        credential.CertificateStoreLocation = StoreLocation.LocalMachine;
        Assert.NotEqual(baseline, MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, TenantId, "https://graph.microsoft.com"));
        credential.CertificateStoreLocation = StoreLocation.CurrentUser;
        credential.CertificateStoreName = StoreName.TrustedPeople;
        Assert.NotEqual(baseline, MicrosoftGraphUtils.BuildGraphTokenCacheKey(credential, TenantId, "https://graph.microsoft.com"));
    }

    internal static X509Certificate2 CreateNonExportableCertificate() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException("This fixture requires Windows CNG.");
        // Framework's CreateSelfSigned copies the key by exporting it. Only this synthetic
        // bootstrap key is exportable; the returned certificate imports without that flag.
        // A null key name keeps the software bootstrap key ephemeral.
        var parameters = new CngKeyCreationParameters {
            Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
            ExportPolicy = CngExportPolicies.AllowExport | CngExportPolicies.AllowPlaintextExport,
            KeyUsage = CngKeyUsages.Signing
        };
        parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
        using CngKey key = CngKey.Create(CngAlgorithm.Rsa, null, parameters);
        using var rsa = new RSACng(key);
        var request = new CertificateRequest("CN=Mailozaurr Graph synthetic test", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 bootstrap = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        const string password = "synthetic-fixture-bootstrap-only";
        byte[] pfx = bootstrap.Export(X509ContentType.Pfx, password);
        try {
            return new X509Certificate2(pfx, password, X509KeyStorageFlags.EphemeralKeySet);
        } finally {
            Array.Clear(pfx, 0, pfx.Length);
        }
    }

    internal static void AssertNonExportable(X509Certificate2 certificate) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException("This assertion requires Windows CNG.");
        using RSA? key = certificate.GetRSAPrivateKey();
        RSACng cng = Assert.IsType<RSACng>(key);
        Assert.True(cng.Key.IsEphemeral);
        Assert.Equal(CngProvider.MicrosoftSoftwareKeyStorageProvider, cng.Key.Provider);
        Assert.Equal(CngExportPolicies.None, cng.Key.ExportPolicy);
        Assert.Throws<CryptographicException>(() => cng.ExportParameters(true));
    }

    internal static void AssertCanSign(X509Certificate2 certificate) {
        byte[] data = Encoding.UTF8.GetBytes("borrowed-key-lifetime-check");
        using RSA? privateKey = certificate.GetRSAPrivateKey();
        using RSA? publicKey = certificate.GetRSAPublicKey();
        Assert.NotNull(privateKey);
        Assert.NotNull(publicKey);
        byte[] signature = privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    private static GraphCredential CreateCredential() => new() {
        ClientId = ClientId,
        DirectoryId = TenantId,
        ClientSecret = SyntheticSecret
    };

    private static void AssertAssertionSignature(string assertion, X509Certificate2 certificate, Uri tokenEndpoint) {
        string[] parts = assertion.Split('.');
        Assert.Equal(3, parts.Length);
        using JsonDocument header = JsonDocument.Parse(DecodeBase64Url(parts[0]));
        using JsonDocument payload = JsonDocument.Parse(DecodeBase64Url(parts[1]));
        string? algorithm = header.RootElement.GetProperty("alg").GetString();
        Assert.Contains(algorithm, new[] { "RS256", "PS256" });
        Assert.Equal(ClientId, payload.RootElement.GetProperty("iss").GetString());
        Assert.Equal(ClientId, payload.RootElement.GetProperty("sub").GetString());
        Assert.Equal(tokenEndpoint.AbsoluteUri, payload.RootElement.GetProperty("aud").GetString());
        Assert.True(long.Parse(payload.RootElement.GetProperty("exp").ToString(), System.Globalization.CultureInfo.InvariantCulture)
            > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using RSA? publicKey = certificate.GetRSAPublicKey();
        Assert.NotNull(publicKey);
        Assert.True(publicKey.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]),
            DecodeBase64Url(parts[2]), HashAlgorithmName.SHA256,
            algorithm == "PS256" ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1));
    }

    private static byte[] DecodeBase64Url(string value) {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }

    private sealed class TokenTransport {
        private readonly Uri _authority;
        private int _requestCount;

        internal TokenTransport(Uri? authority = null) {
            _authority = authority ?? new Uri("https://login.microsoftonline.com/");
            TokenEndpoint = new Uri(_authority, TenantId + "/oauth2/v2.0/token");
        }

        internal Uri TokenEndpoint { get; }
        internal List<Dictionary<string, string>> Forms { get; } = new();
        internal List<Uri> TokenUris { get; } = new();
        internal int RequestCount => _requestCount;
        internal Func<int, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (call, _) => Task.FromResult(TokenResponse("token-" + call, 3600));

        internal async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken) {
            Interlocked.Increment(ref _requestCount);
            using HttpRequestMessage request = createRequest();
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal)) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(JsonSerializer.Serialize(new {
                        authorization_endpoint = new Uri(_authority, TenantId + "/oauth2/v2.0/authorize").AbsoluteUri,
                        token_endpoint = TokenEndpoint.AbsoluteUri,
                        issuer = new Uri(_authority, TenantId + "/v2.0").AbsoluteUri,
                        jwks_uri = new Uri(_authority, TenantId + "/discovery/v2.0/keys").AbsoluteUri
                    }), Encoding.UTF8, "application/json")
                };
            }
            if (request.Method != HttpMethod.Post || request.RequestUri != TokenEndpoint) {
                throw new InvalidOperationException("Unexpected fake authentication endpoint.");
            }
            string body = await request.Content!.ReadAsStringAsync();
            Dictionary<string, string> form = body.Split('&').ToDictionary(
                field => WebUtility.UrlDecode(field.Substring(0, field.IndexOf('='))),
                field => WebUtility.UrlDecode(field.Substring(field.IndexOf('=') + 1)));
            int call;
            lock (Forms) {
                Forms.Add(form);
                TokenUris.Add(request.RequestUri!);
                call = Forms.Count;
            }
            return await Respond(call, cancellationToken);
        }

        internal static HttpResponseMessage TokenResponse(string token, int expiresIn) => new(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(new {
                access_token = token,
                token_type = "Bearer",
                expires_in = expiresIn,
                ext_expires_in = expiresIn
            }), Encoding.UTF8, "application/json")
        };
    }
}
