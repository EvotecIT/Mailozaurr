Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Forward IMAP messages without attachments
$imapCred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $imapCred -Port 993 -Options Auto
Get-IMAPMessage -Client $imap -FromContains 'reports@example.com' -HasAttachment |
    ForEach-Object {
        $forward = $_.MimeMessage | Remove-IMAPMessageAttachment
        Send-EmailMessage -From 'forwarder@example.com' -To 'team@example.com' -Subject $_.MimeMessage.Subject -Message $forward -Server 'smtp.example.com' -Port 25 -WhatIf
    }
Disconnect-IMAP -Client $imap

# Forward POP3 messages without attachments
$popCred = Get-Credential
$pop = Connect-POP3 -Server 'pop.example.com' -Credential $popCred -Port 995 -Options Auto
Get-POP3Message -Client $pop -Subject 'Alert' -HasAttachment |
    ForEach-Object {
        $forward = $_.MimeMessage | Remove-POP3MessageAttachment
        Send-EmailMessage -From 'forwarder@example.com' -To 'team@example.com' -Subject $_.MimeMessage.Subject -Message $forward -Server 'smtp.example.com' -Port 25 -WhatIf
    }
Disconnect-POP3 -Client $pop

# Forward Microsoft Graph messages without attachments
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'
$graphCred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
Connect-EmailGraph -Credential $graphCred | Out-Null
Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -Filter "hasAttachments eq true" -Limit 5 |
    ForEach-Object {
        $forward = Remove-GraphMessageAttachment -Message $_
        Send-EmailMessage -From 'forwarder@example.com' -To 'team@example.com' `
            -Subject $forward.Subject -Body $forward.Body.Content -Graph -Credential $graphCred -WhatIf
    }
Disconnect-EmailGraph
