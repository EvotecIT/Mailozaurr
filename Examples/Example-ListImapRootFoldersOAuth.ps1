Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Acquire OAuth token first
$token = Connect-OAuthGoogle -ClientId 'id' -ClientSecret 'secret' -GmailAccount 'user@example.com' -Scope https://mail.google.com/

# Connect using the OAuth token and list folders
$client = Connect-IMAP -Server 'imap.gmail.com' -Port 993 -Credential $token -OAuth2
Get-IMAPFolder -Client $client -Root

Disconnect-IMAP -Client $client
