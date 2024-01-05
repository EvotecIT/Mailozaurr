Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Result = Get-IPGeolocation -IPAddress '40.107.22.71' -Verbose
$Result