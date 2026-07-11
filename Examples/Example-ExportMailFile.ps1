Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$OutputPath = "$PSScriptRoot\Output\Sample.msg"
Import-MailFile "$PSScriptRoot\Input\Sample.eml" |
    Export-MailFile $OutputPath -Force -PassThru
