Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Strip attachments from IMAP messages sent by alerts@example.com in the last week
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto
Get-IMAPMessage -Client $imap -FromContains 'alerts@example.com' -Since (Get-Date).AddDays(-7) -HasAttachment |
    ForEach-Object {
        $updated = $_.MimeMessage | Remove-IMAPMessageAttachment
        $path = Join-Path $PSScriptRoot "IMAP-$($_.Uid.Id).eml"
        $updated.WriteTo($path)
    }
Disconnect-IMAP -Client $imap

# Strip attachments from POP3 messages with subject 'Report'
$popCred = Get-Credential
$pop = Connect-POP3 -Server 'pop.example.com' -Credential $popCred -Port 995 -Options Auto
Get-POP3Message -Client $pop -Subject 'Report' -HasAttachment |
    ForEach-Object {
        $updated = $_.MimeMessage | Remove-POP3MessageAttachment
        $path = Join-Path $PSScriptRoot "POP3-$($_.Index).eml"
        $updated.WriteTo($path)
    }
Disconnect-POP3 -Client $pop

# Strip attachments from Graph messages from the same sender
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'
$graphCred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
Connect-EmailGraph -Credential $graphCred | Out-Null
Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -Filter "from/emailAddress/address eq 'alerts@example.com' and hasAttachments eq true" -Limit 5 |
    ForEach-Object {
        $updated = Remove-GraphMessageAttachment -Message $_
        Save-GraphMessage -Message $updated -Path (Join-Path $PSScriptRoot 'Graph')
    }
Disconnect-EmailGraph
