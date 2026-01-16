Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$tmp = New-TemporaryFile
Set-Content -Path $tmp -Value "data"
$html = "<img src='$tmp'><p>$tmp</p>"

$result = [Mailozaurr.HtmlUtils]::ExtractLocalImagePaths($html)
$result.Html
$result.Paths

Remove-Item $tmp

