Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Conversion = ConvertFrom-EmlToMsg -InputPath "$PSScriptRoot\Input\Sample.eml" -OutputFolder "$PSScriptRoot\Output" -Verbose -Force
$Conversion | Format-Table
if ($Conversion.Status) {
    $Msg = Import-MailFile $Conversion.MsgFile
    $Msg | Format-Table
}
