Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Create a throwaway PGP key pair using the unified cmdlet
$keys = New-TemporaryMailCrypto -Pgp

# Send an encrypted and signed message (simulation)
Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' `
    -Server 'smtp.example.com' -Port 25 -Subject 'Temporary PGP test' `
    -Body 'Hello from temporary PGP keys' -PublicKeyPath $keys.PublicKeyPath `
    -PrivateKeyPath $keys.PrivateKeyPath -PrivateKeyPassword $keys.PassPhrase `
    -SignOrEncrypt PgpSignAndEncrypt -WhatIf -Verbose

# Use the returned key paths with Unprotect-MimeMessage to decrypt received messages.
