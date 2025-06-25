Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Example: Download attachments and archive the message
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

$messages = Get-GraphMessage -UserPrincipalName 'user@example.com' -Connection $graph -Filter "hasAttachments eq true" -Limit 5
foreach ($m in $messages) {
    Get-MailMessageAttachment -UserPrincipalName 'user@example.com' -MessageId $m.Id -Connection $graph |
        Save-GraphMessageAttachment -Path 'C:\Temp\Attachments'
    Set-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -Connection $graph -Read
    Move-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -DestinationFolderId 'Archive' -Connection $graph
}

Disconnect-EmailGraph -Connection $graph
