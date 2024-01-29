Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Msg = Import-MailFile -FilePath "$PSScriptRoot\Input\TestMessage.msg"
$Msg | Format-List *