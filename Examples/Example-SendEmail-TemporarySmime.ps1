Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Generate a temporary certificate and export it
$pfx = Join-Path $env:TEMP 'tempcert.pfx'
$cert = New-TemporaryMailCrypto -Smime -OutputPath $pfx

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' `
    -Server 'smtp.example.com' -Port 25 -Subject 'Temporary cert test' `
    -Body 'Hello from temporary certificate' -Certificate $cert `
    -SignOrEncrypt SmimeSignAndEncrypt -WhatIf -Verbose
