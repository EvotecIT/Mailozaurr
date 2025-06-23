Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Example: Download attachments and archive the message
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-Graph -Credential $cred

$messages = Get-EmailMessage -UserPrincipalName 'user@example.com' -Connection $graph -Graph -Filter "hasAttachments eq true" -Limit 5
foreach ($m in $messages) {
    Get-MailMessageAttachment -UserPrincipalName 'user@example.com' -MessageId $m.Id -Credential $cred |
        Save-MailMessageAttachment -Path 'C:\Temp\Attachments'
    Set-MailMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -Connection $graph -Graph -Read
    Move-MailMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -DestinationFolderId 'Archive' -Connection $graph -Graph
}
