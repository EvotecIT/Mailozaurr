using Microsoft.Identity.Client;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Mailozaurr;

/// <summary>Authenticates an application to Graph using MSAL and a private, memory-only token cache.</summary>
/// <remarks>Certificate instances and supplied transports remain caller-owned. Dispose after all requests complete.</remarks>
public sealed class GraphAuthenticationClient : IDisposable {
    private readonly IConfidentialClientApplication _application;
    private readonly X509Certificate2? _ownedCertificate;
    private readonly HttpClient? _httpClient;
    private readonly string[] _scopes;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a client, preferring certificate instance, store, file, bytes, then PEM over a client secret.</summary>
    /// <param name="credential">Application identity and credentials. A supplied certificate is borrowed.</param>
    /// <param name="authorityHost">The HTTPS Entra authority host; defaults to the global authority.</param>
    /// <param name="send">Optional HTTP pipeline. Each factory creates a fresh request for retries.</param>
    /// <param name="scopes">Requested scopes; defaults to the Graph application scope.</param>
    public GraphAuthenticationClient(GraphCredential credential, Uri? authorityHost = null,
        Func<Func<HttpRequestMessage>, CancellationToken, Task<HttpResponseMessage>>? send = null,
        IEnumerable<string>? scopes = null) {
        if (credential == null) throw new ArgumentNullException(nameof(credential));
        if (string.IsNullOrWhiteSpace(credential.ClientId)) throw new ArgumentException("A client ID is required.", nameof(credential));
        if (string.IsNullOrWhiteSpace(credential.DirectoryId)) throw new ArgumentException("A tenant ID is required.", nameof(credential));
        authorityHost ??= new Uri("https://login.microsoftonline.com/");
        if (!authorityHost.IsAbsoluteUri || authorityHost.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(authorityHost.UserInfo) ||
            !string.IsNullOrEmpty(authorityHost.Query) || !string.IsNullOrEmpty(authorityHost.Fragment)) {
            throw new ArgumentException("An HTTPS authority without credentials, query or fragment is required.", nameof(authorityHost));
        }
        string[] normalizedScopes = OAuthHelpers.NormalizeScopes(scopes ?? Array.Empty<string>());
        _scopes = normalizedScopes.Length == 0 ? new[] { "https://graph.microsoft.com/.default" } : normalizedScopes;
        try {
            var builder = ConfidentialClientApplicationBuilder.Create(credential.ClientId)
                .WithAuthority(new Uri(authorityHost, Uri.EscapeDataString(credential.DirectoryId)).AbsoluteUri)
                .WithInstanceDiscovery(false);
            X509Certificate2? certificate = credential.Certificate;
            if (certificate == null) certificate = _ownedCertificate = LoadCertificate(credential);
            if (certificate != null) {
                if (!certificate.HasPrivateKey) throw new InvalidOperationException();
                builder = builder.WithCertificate(certificate);
            } else {
                if (string.IsNullOrWhiteSpace(credential.ClientSecret)) throw new InvalidOperationException();
                builder = builder.WithClientSecret(credential.ClientSecret);
            }
            if (send != null) {
                _httpClient = new HttpClient(new PipelineHandler(send)) { Timeout = Timeout.InfiniteTimeSpan };
                // The supplied pipeline owns its attempt budget and delay policy.
                builder = builder.WithHttpClientFactory(new ClientFactory(_httpClient), retryOnceOn5xx: false);
            }
            _application = builder.Build();
        } catch (Exception) {
            _ownedCertificate?.Dispose();
            _httpClient?.Dispose();
            throw new InvalidOperationException(MicrosoftGraphUtils.AuthenticationFailure);
        }
    }

    /// <summary>Returns a cached authorization or acquires a new one; provider error content is never exposed.</summary>
    /// <param name="cancellationToken">Cancels waiting, signing in and HTTP requests.</param>
    /// <returns>The bearer token and its expiry.</returns>
    public async Task<GraphAuthorization> GetAuthorizationAsync(CancellationToken cancellationToken = default) {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            AuthenticationResult result = await _application.AcquireTokenForClient(_scopes).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new GraphAuthorization { AccessToken = result.AccessToken, TokenType = "Bearer", ExpiresOn = result.ExpiresOn };
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException("Microsoft Graph authentication canceled.", cancellationToken);
        } catch (Exception) {
            throw new InvalidOperationException(MicrosoftGraphUtils.AuthenticationFailure);
        } finally {
            _gate.Release();
        }
    }

    /// <summary>Releases owned certificates and the HTTP adapter, leaving borrowed credentials and transports intact.</summary>
    public void Dispose() {
        _httpClient?.Dispose();
        _ownedCertificate?.Dispose();
        _gate.Dispose();
    }

    private static X509Certificate2? LoadCertificate(GraphCredential credential) {
        if (!string.IsNullOrWhiteSpace(credential.CertificateThumbprint)) {
            return GraphCertificateStore.Load(credential.CertificateThumbprint!, credential.CertificateStoreLocation, credential.CertificateStoreName);
        }
        if (!string.IsNullOrWhiteSpace(credential.CertificatePath)) return CertificateDataLoader.LoadPkcs12File(credential.CertificatePath!, credential.CertificatePassword, GraphCertificateStore.ImportKeyStorageFlags);
        if (credential.CertificateBytes != null) return CertificateDataLoader.LoadPkcs12(credential.CertificateBytes, credential.CertificatePassword, GraphCertificateStore.ImportKeyStorageFlags);
        if (!string.IsNullOrWhiteSpace(credential.CertificatePemPath)) {
#if NET5_0_OR_GREATER
            return X509Certificate2.CreateFromPemFile(credential.CertificatePemPath!);
#else
            throw new NotSupportedException("PEM certificates are not supported on this framework.");
#endif
        }
        return null;
    }

    private sealed class ClientFactory(HttpClient client) : IMsalHttpClientFactory {
        public HttpClient GetHttpClient() => client;
    }

    private sealed class PipelineHandler(Func<Func<HttpRequestMessage>, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            byte[]? body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return await send(() => {
                var copy = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
                foreach (var header in request.Headers) copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
                if (body != null) {
                    copy.Content = new ByteArrayContent(body);
                    foreach (var header in request.Content!.Headers) copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                return copy;
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
