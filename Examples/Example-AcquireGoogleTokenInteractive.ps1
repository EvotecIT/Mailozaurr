Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$GmailAccount = 'user@gmail.com'
$ClientID = 'Your-Client-ID'
$ClientSecret = Read-Host 'Google client secret' -AsSecureString
$Scopes = @('https://mail.google.com/')

$Credential = Connect-OAuthGoogle -GmailAccount $GmailAccount -ClientID $ClientID -ClientSecretSecureString $ClientSecret -Scope $Scopes

$CredInfo = ConvertFrom-OAuth2Credential -Credential $Credential
$CredInfo | Format-List

# Send a test email using the token
Send-EmailMessage -From $GmailAccount -To 'recipient@example.com' `
    -Server 'smtp.gmail.com' -Subject 'Test OAuth email' -Text 'Hello' `
    -SecureSocketOptions Auto -Credential $Credential -OAuth2

# Connect to Gmail via IMAP
$ImapClient = Connect-IMAP -Server 'imap.gmail.com' -Port 993 -Options Auto `
    -Credential $Credential -OAuth2
Get-IMAPFolder -Client $ImapClient -Verbose
Disconnect-IMAP -Client $ImapClient

# And POP3
$PopClient = Connect-POP3 -Server 'pop.gmail.com' -Port 995 -Options Auto `
    -Credential $Credential -OAuth2
Disconnect-POP3 -Client $PopClient
