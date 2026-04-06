Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$clientSecret = Read-Host 'Google client secret' -AsSecureString
$cred = Connect-OAuthGoogle -GmailAccount 'user@gmail.com' -ClientID 'id' -ClientSecretSecureString $clientSecret -Scope https://mail.google.com/

Get-DmarcReport -Protocol GmailApi -GmailAccount 'user@gmail.com' -Credential $cred -Since (Get-Date).AddDays(-7) |
    ForEach-Object {
        foreach ($att in $_.Attachments) {
            # Pass the zipped XML stream to Domain Detective for analysis
            # Invoke-DomainDetective -InputStream $att.Content -Name $att.Name
        }
    }
