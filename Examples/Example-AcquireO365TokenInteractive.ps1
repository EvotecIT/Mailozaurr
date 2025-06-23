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

# Use the OAuth token to send mail via SMTP
Send-EmailMessage -From $Login -To 'recipient@example.com' `
    -Server 'smtp.office365.com' -Subject 'Test OAuth email' -Text 'Hello' `
    -SecureSocketOptions Auto -Credential $Credential -OAuth2

# Use the same credential with IMAP
$ImapClient = Connect-IMAP -Server 'outlook.office365.com' -Port 993 -Options Auto `
    -Credential $Credential -OAuth2
Get-IMAPFolder -Client $ImapClient -Verbose
Disconnect-IMAP -Client $ImapClient

# And with POP3
$PopClient = Connect-POP3 -Server 'outlook.office365.com' -Port 995 -Options Auto `
    -Credential $Credential -OAuth2
Disconnect-POP3 -Client $PopClient
