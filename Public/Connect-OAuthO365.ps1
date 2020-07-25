function Connect-oAuthO365 {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ClientID,
        [Parameter(Mandatory)][string] $TenantID,
        [uri] $RedirectUri = 'https://login.microsoftonline.com/common/oauth2/nativeclient',
        [ValidateSet(
            "email",
            "offline_access",
            "https://outlook.office.com/IMAP.AccessAsUser.All",
            "https://outlook.office.com/POP.AccessAsUser.All",
            "https://outlook.office.com/SMTP.Send"
        )][string[]] $Scopes = @(
            "email",
            "offline_access",
            "https://outlook.office.com/IMAP.AccessAsUser.All",
            "https://outlook.office.com/POP.AccessAsUser.All",
            "https://outlook.office.com/SMTP.Send"
        )
    )
    $Options = [Microsoft.Identity.Client.PublicClientApplicationOptions]::new()
    $Options.ClientId = $ClientID
    $Options.TenantId = $TenantID
    $Options.RedirectUri = $RedirectUri

    $PublicClientApplication = [Microsoft.Identity.Client.PublicClientApplicationBuilder]::CreateWithApplicationOptions($Options).Build()

    # https://docs.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth
    try {
        $AuthToken = $PublicClientApplication.AcquireTokenInteractive($Scopes).ExecuteAsync() | Wait-Task
        $oAuth2 = [MailKit.Security.SaslMechanismOAuth2]::new($AuthToken.Account.Username, $AuthToken.AccessToken)
        $oAuth2
    } catch {
        Write-Warning "Connect-oAuth - $_"
    }
}
