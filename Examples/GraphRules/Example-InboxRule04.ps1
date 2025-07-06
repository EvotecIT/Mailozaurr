Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$graph = Connect-EmailGraph -Credential (ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant')
$rule = Get-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -Filter "displayName eq 'Move Boss Mail'" | Select-Object -First 1
if ($rule) {
    $rule.MoveToFolder = 'Inbox'
    Set-GraphInboxRule -UserPrincipalName 'user@example.com' -RuleId $rule.Id -Connection $graph -RuleObject $rule
}
