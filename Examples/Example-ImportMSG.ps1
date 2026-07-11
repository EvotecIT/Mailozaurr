Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Msg = Import-MailFile "$PSScriptRoot\Input\TestMessage.msg"
$Msg | Format-Table

$Eml = Import-MailFile "$PSScriptRoot\Input\Sample.eml"
$Eml | Format-Table
