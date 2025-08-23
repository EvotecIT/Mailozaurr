Import-Module Mailozaurr

# Assumes an active IMAP connection established with Connect-IMAP
Get-DmarcReport -Protocol Imap -Folder 'INBOX' -Since (Get-Date).AddDays(-7) |
    ForEach-Object {
        foreach ($att in $_.Attachments) {
            # Pass the zipped XML to Domain Detective for analysis
            Invoke-DomainDetective -InputObject $att.Content -Name $att.Name
        }
    }
