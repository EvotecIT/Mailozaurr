Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Example: Download attachments and archive the message
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
Connect-EmailGraph -Credential $cred | Out-Null

$messages = Get-GraphMessage -UserPrincipalName 'user@example.com' -Filter "hasAttachments eq true" -Limit 5
foreach ($m in $messages) {
    Get-MailMessageAttachment -UserPrincipalName 'user@example.com' -MessageId $m.Id |
        Save-GraphMessageAttachment -Path 'C:\Temp\Attachments'
    Set-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -Read
    Move-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -DestinationFolderId 'Archive'
}
Disconnect-EmailGraph
