Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$tmp = New-TemporaryFile
Set-Content -Path $tmp -Value "data"
$html = "<img src='$tmp'><p>$tmp</p>"

$result = ConvertFrom-HtmlLocalImagePath -Html $html
$result.Html
$result.Paths

Remove-Item $tmp
