Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$graph = Connect-EmailGraph -Credential (ConvertTo-GraphCredential -ClientId $cred.UserName -ClientSecret $cred.GetNetworkCredential().Password -DirectoryId 'tenant')

$rule = Get-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -Filter "displayName eq 'Move Boss Mail'" | Select-Object -First 1
if ($rule) {
    $rule.Actions.MoveToFolder = 'Inbox'
    Set-GraphInboxRule -UserPrincipalName 'user@example.com' -RuleId $rule.Id -Connection $graph -RuleObject $rule
}

Disconnect-EmailGraph
