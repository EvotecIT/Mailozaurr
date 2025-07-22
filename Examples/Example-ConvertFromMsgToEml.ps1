Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Conversion = ConvertFrom-MsgToEml -InputPath "$PSScriptRoot\Input\Sample.msg" -OutputFolder "$PSScriptRoot\Output" -Verbose -Force
$Conversion | Format-Table
if ($Conversion.Status) {
    $Eml = Import-MailFile -FilePath $Conversion.EmlFile
    $Eml | Format-Table
    $Eml.Dispose()
}
