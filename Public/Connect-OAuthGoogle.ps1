function Connect-oAuthGoogle {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $GmailAccount,
        [Parameter(Mandatory)][string] $ClientID,
        [Parameter(Mandatory)][string] $ClientSecret,
        [ValidateSet("https://mail.google.com/")][string[]] $Scope = @("https://mail.google.com/")
    )

    $ClientSecrets = [Google.Apis.Auth.OAuth2.ClientSecrets]::new()
    $ClientSecrets.ClientId = $ClientID
    $ClientSecrets.ClientSecret = $ClientSecret

    $Initializer = [Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow+Initializer]::new()
    $Initializer.DataStore = [Google.Apis.Util.Store.FileDataStore]::new("CredentialCacheFolder", $false)
    $Initializer.Scopes = $Scope
    $Initializer.ClientSecrets = $ClientSecrets

    $CodeFlow = [Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow]::new($Initializer)

    $codeReceiver = [Google.Apis.Auth.OAuth2.LocalServerCodeReceiver]::new()
    $AuthCode = [Google.Apis.Auth.OAuth2.AuthorizationCodeInstalledApp]::new($CodeFlow, $codeReceiver)
    $Credential = $AuthCode.AuthorizeAsync($GmailAccount, [System.Threading.CancellationToken]::None) | Wait-Task

    if ($Credential.Token.IsExpired([Google.Apis.Util.SystemClock]::Default)) {
        $credential.RefreshTokenAsync([System.Threading.CancellationToken]::None) | Wait-Task
    }
    $oAuth2 = [MailKit.Security.SaslMechanismOAuth2]::new($credential.UserId, $credential.Token.AccessToken)
    $oAuth2
}
