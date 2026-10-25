# OAuth authentication flows

Mailozaurr can acquire delegated Microsoft Graph tokens using additional OAuth flows.

## Device code authentication

Use `Connect-EmailGraph` with the `-DeviceCode` switch to sign in with a device code. Provide the application ID, tenant ID and scopes.

```powershell
$scopes = @('Mail.ReadWrite','Mail.Send')
$graph = Connect-EmailGraph -ClientId '<app id>' -DirectoryId '<tenant id>' -DeviceCode -Scopes $scopes
```

Follow the instructions printed on the console to complete authentication.

## On-behalf-of token exchange

If you already have a user access token, exchange it for Microsoft Graph scopes using `-OnBehalfOfToken`.

```powershell
$token = Get-Content -Raw '.\UserToken.txt'
$graph = Connect-EmailGraph -ClientId '<app id>' -ClientSecret '<secret>' -DirectoryId '<tenant id>' `
    -OnBehalfOfToken $token -Scopes $scopes
```

Both methods return a `GraphConnectionInfo` object with the OAuth credential available in the `OAuthCredential` property.

## Application authentication in .NET

`GraphAuthenticationClient` uses MSAL to sign in with a client secret or certificate and keeps tokens in memory for the
client's lifetime. It accepts a caller-owned `X509Certificate2`, including a Windows-store certificate whose private key
cannot be exported. The key stays with its provider; Mailozaurr does not export or reimport it.

```csharp
using Mailozaurr;
using System.Security.Cryptography.X509Certificates;

using var certificate = GraphCertificateStore.Load(
    thumbprint, StoreLocation.LocalMachine, StoreName.My);
using var authentication = new GraphAuthenticationClient(new GraphCredential {
    ClientId = clientId,
    DirectoryId = tenantId,
    Certificate = certificate
});
GraphAuthorization authorization = await authentication.GetAuthorizationAsync(cancellationToken);
```

Alternatively, set `CertificateThumbprint`, `CertificateStoreLocation` and `CertificateStoreName` on `GraphCredential`;
the client then resolves and disposes the certificate itself. `CertificatePath` plus `CertificatePassword`,
`CertificateBytes` plus `CertificatePassword`, and `CertificatePemPath` remain available. PEM loading requires .NET 5 or
later. A supplied certificate takes precedence over the store, file, bytes and PEM options, which take precedence over
`ClientSecret`. A configured certificate that cannot be used fails authentication instead of falling back to a secret.

An optional HTTPS authority and HTTP request factory pipeline let services keep their own HTTP retry policies. Each
factory invocation returns a fresh request. Cancellation reaches both token acquisition and the pipeline; authentication
failures expose a generic message without provider response bodies, credentials or inner exceptions. Dispose the client
after its requests complete. It leaves supplied certificates and transports owned by the caller.

For a single acquisition, `OAuthHelpers.AcquireGraphCertificateTokenAsync` also accepts an `X509Certificate2` and a
cancellation token. Keep a `GraphAuthenticationClient` when repeated calls should reuse its memory cache.
