Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$path = Join-Path $PSScriptRoot 'duplicate.txt'
'data' | Set-Content -Path $path

Send-EmailMessage -From 'example@example.com' -To 'recipient@example.com' -Subject 'Dedup attachments' -Body 'Demo' -Server 'smtp.example.com' -Port 25 -Attachment $path,$path -WhatIf

Remove-Item $path
