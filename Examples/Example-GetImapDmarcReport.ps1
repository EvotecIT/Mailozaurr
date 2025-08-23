Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential) | Out-Null
Get-DmarcReport -Protocol Imap -Folder 'INBOX' -Since (Get-Date).AddDays(-7) |
    ForEach-Object {
        foreach ($att in $_.Attachments) {
            # Pass the zipped XML to Domain Detective for analysis
            # Invoke-DomainDetective -InputObject $att.Content -Name $att.Name
        }
    }
Disconnect-IMAP
