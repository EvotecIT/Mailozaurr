Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

# Build a rule that moves messages from the boss to Archive and stops processing
$builder = New-GraphInboxRuleBuilder -DisplayName 'Boss Archive' -Sequence 1 -SenderContains 'boss@example.com' -MoveToFolder 'Archive' -StopProcessing
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -RuleBuilder $builder

# Build and create a rule that forwards urgent messages
$forward = New-GraphInboxRuleBuilder -DisplayName 'Forward Urgent' -Sequence 2 -SubjectContains 'urgent' -ForwardTo 'assistant@example.com' -Enabled
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -RuleBuilder $forward

# Build and create a rule that deletes spam
$spam = New-GraphInboxRuleBuilder -DisplayName 'Delete Spam' -Sequence 3 -SenderContains 'spam@example.com' -Delete
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -RuleBuilder $spam
