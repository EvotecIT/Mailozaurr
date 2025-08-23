Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Connect-POP3 -Server 'pop3.example.com' -Credential (Get-Credential) | Out-Null
Get-DmarcReport -Protocol Pop3 -Since (Get-Date).AddDays(-7) |
    ForEach-Object {
        foreach ($att in $_.Attachments) {
            # Pass the zipped XML to Domain Detective for analysis
            # Invoke-DomainDetective -InputObject $att.Content -Name $att.Name
        }
    }
Disconnect-POP3
