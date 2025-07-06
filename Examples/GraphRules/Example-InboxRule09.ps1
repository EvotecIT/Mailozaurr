Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$rule = New-GraphInboxRuleObject -DisplayName 'Move Boss Mail' -Sequence 1 -Enabled -SenderContains 'boss@example.com' -MoveToFolder 'Archive'
