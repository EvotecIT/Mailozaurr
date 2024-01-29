Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Conversion = ConvertFrom-EmlToMsg -InputPath "$PSScriptRoot\Input\Sample.eml" -OutputPath "$PSScriptRoot\Output\Sample.msg"

if ($Conversion.Status) {
    $Msg = Import-MailFile -FilePath $Conversion.OutputPath
    $Msg | Format-List *
}