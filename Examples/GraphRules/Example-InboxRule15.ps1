Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$graph = Connect-EmailGraph -Credential (ConvertTo-GraphCredential -ClientId $cred.UserName -ClientSecret $cred.GetNetworkCredential().Password -DirectoryId 'tenant')

$rule = New-GraphInboxRuleObject -DisplayName 'Move Boss Mail' -Sequence 1 -Enabled -SenderContains 'boss@example.com' -MoveToFolder 'Archive'
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -RuleObject $rule
