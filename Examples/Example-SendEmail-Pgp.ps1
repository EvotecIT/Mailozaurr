Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$pub = Join-Path $PSScriptRoot 'PGPKeys/mimekit.gpg.pub'
$sec = Join-Path $PSScriptRoot 'PGPKeys/mimekit.gpg.sec'

Send-EmailMessage -From 'mimekit@example.com' -To 'mimekit@example.com' \
    -Server 'smtp.example.com' -Port 25 -Subject 'PGP Test' -Body 'Hello' \
    -SignOrEncrypt PgpSignAndEncrypt -PublicKeyPath $pub -PrivateKeyPath $sec \
    -PrivateKeyPassword 'no.secret' -WhatIf -Verbose
