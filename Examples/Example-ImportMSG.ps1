Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Msg = Import-MailFile -FilePath "$PSScriptRoot\Input\TestMessage.msg"
$Msg | Format-Table

$Eml = Import-MailFile -FilePath "$PSScriptRoot\Input\Sample.eml"
$Eml | Format-Table