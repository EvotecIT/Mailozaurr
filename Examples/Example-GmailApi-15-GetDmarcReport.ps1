Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Connect-OAuthGoogle -GmailAccount 'user@gmail.com' -ClientID 'id' -ClientSecret 'secret' -Scope https://mail.google.com/

Get-DmarcReport -Protocol GmailApi -GmailAccount 'user@gmail.com' -Credential $cred -Since (Get-Date).AddDays(-7) |
    ForEach-Object {
        foreach ($att in $_.Attachments) {
            # Pass the zipped XML stream to Domain Detective for analysis
            # Invoke-DomainDetective -InputStream $att.Content -Name $att.Name
        }
    }
