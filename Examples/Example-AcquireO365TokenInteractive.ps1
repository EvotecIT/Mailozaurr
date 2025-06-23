Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Login = 'user@example.com'
$ClientID = 'Your-Application-ID'
$TenantID = 'Your-Tenant-ID'
$RedirectUri = 'https://login.microsoftonline.com/common/oauth2/nativeclient'
$Scopes = @(
    'email',
    'offline_access',
    'https://outlook.office.com/IMAP.AccessAsUser.All',
    'https://outlook.office.com/POP.AccessAsUser.All',
    'https://outlook.office.com/SMTP.Send'
)

$Credential = Connect-OAuthO365 -Login $Login -ClientID $ClientID -TenantID $TenantID -RedirectUri $RedirectUri -Scopes $Scopes

$CredInfo = ConvertFrom-OAuth2Credential -Credential $Credential
$CredInfo | Format-List
