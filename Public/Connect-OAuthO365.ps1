function Connect-oAuthO365 {
    [cmdletBinding()]
    param(
        [string] $Login,
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

    try {
        $PublicClientApplication = [Microsoft.Identity.Client.PublicClientApplicationBuilder]::CreateWithApplicationOptions($Options).Build()
    } catch {
        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            Write-Error $_
            return
        } else {
            Write-Warning "Connect-oAuthO365 - Error: $($_.Exception.Message)"
            return
        }
    }

    # https://www.powershellgallery.com/packages/MSAL.PS/4.2.1.1/Content/Get-MsalToken.ps1
    # Here we should implement something for Silent Token
    # $Account = $Account
    # $AuthToken = $PublicClientApplication.AcquireTokenSilent($Scopes, $login).ExecuteAsync([System.Threading.CancellationToken]::None) | Wait-Task
    # $oAuth2 = [MailKit.Security.SaslMechanismOAuth2]::new($AuthToken.Account.Username, $AuthToken.AccessToken)

    # https://docs.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth
    try {
        if ($Login) {
            $AuthToken = $PublicClientApplication.AcquireTokenInteractive($Scopes).ExecuteAsync([System.Threading.CancellationToken]::None) | Wait-Task
        } else {
            $AuthToken = $PublicClientApplication.AcquireTokenInteractive($Scopes).WithLoginHint($Login).ExecuteAsync([System.Threading.CancellationToken]::None) | Wait-Task
        }
        # Here we should save the AuthToken.Account somehow, somewhere
        # $AuthToken.Account | Export-Clixml -Path $Env:USERPROFILE\Desktop\test.xml -Depth 2
        #[PSCustomObject] @{
        #    UserName = $AuthToken.Account.UserName
        #    Token    = $AuthToken.AccessToken
        #}
        ConvertTo-OAuth2Credential -UserName $AuthToken.Account.UserName -Token $AuthToken.AccessToken

        #$oAuth2 = [MailKit.Security.SaslMechanismOAuth2]::new($AuthToken.Account.Username, $AuthToken.AccessToken)
        #$oAuth2
    } catch {
        Write-Warning "Connect-oAuth - $_"
    }
}