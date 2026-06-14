Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Create a throwaway PGP key pair using the unified cmdlet
$keys = New-TemporaryMailCrypto -Pgp

# Send an encrypted and signed message (simulation)
Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' `
    -Server 'smtp.example.com' -Port 25 -Subject 'Temporary PGP test' `
    -Body 'Hello from temporary PGP keys' -PublicKeyPath $keys.PublicKeyPath `
    -PrivateKeyPath $keys.PrivateKeyPath -PrivateKeyPassword $keys.PassPhrase `
    -SignOrEncrypt PgpSignAndEncrypt -WhatIf -Verbose

# Demonstrate decrypting the message using the same keys
$smtp = [Mailozaurr.Smtp]::new()
$smtp.From = 'sender@example.com'
$smtp.To   = @('sender@example.com')
$smtp.Subject = 'Test message'
$smtp.TextBody = 'Secret text'
$smtp.CreateMessage()
$null = $smtp.PgpEncrypt($keys.PublicKeyPath)
$path = Join-Path $env:TEMP 'test.eml'
$smtp.Message.WriteTo($path)
$plaintext = $keys.DecryptToString($path)
Write-Host "Decrypted body: $plaintext"
