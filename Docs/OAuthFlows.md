# OAuth device code and on-behalf-of flows

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
