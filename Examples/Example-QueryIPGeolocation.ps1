Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Result = Get-IPGeolocation -IPAddress '40.107.22.71', "1.1.1.1" -Verbose
$Result | Format-Table