Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Download attachments then delete the original message via Graph
$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $cred | Out-Null
$messages = Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -Filter "hasAttachments eq true" -Limit 5
foreach ($m in $messages) {
    Get-EmailGraphMessageAttachment -UserPrincipalName 'user@example.com' -MessageId $m.Id |
        Save-GraphMessageAttachment -Path 'C:\Temp\Attachments'
    Remove-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -Confirm:$false
}
Disconnect-EmailGraph
