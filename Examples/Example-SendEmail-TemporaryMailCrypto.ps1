Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Generate temporary PGP keys and keep them on disk
$out = Join-Path $env:TEMP 'pgp-keys'
$keys = New-TemporaryMailCrypto -Pgp -OutputPath $out -NoDispose

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' `
    -Server 'smtp.example.com' -Port 25 -Subject 'Temporary PGP test' `
    -Body 'Hello from temporary PGP keys' -PublicKeyPath $keys.PublicKeyPath `
    -PrivateKeyPath $keys.PrivateKeyPath -PrivateKeyPassword $keys.PassPhrase `
    -SignOrEncrypt PgpSignAndEncrypt -WhatIf -Verbose

# Optionally verify the encrypted message
$smtp = [Mailozaurr.Smtp]::new()
$smtp.From = 'sender@example.com'
$smtp.To   = @('sender@example.com')
$smtp.Subject = 'Test message'
$smtp.TextBody = 'Secret text'
$smtp.CreateMessage()
$null = $smtp.PgpEncrypt($keys.PublicKeyPath)
$path = Join-Path $env:TEMP 'test.eml'
$smtp.Message.WriteTo($path)
$decrypted = $keys.DecryptToString($path)
Write-Verbose "Decrypted body: $decrypted"

# Dispose without deleting
$keys.Dispose()

# Generate temporary S/MIME certificate
$cert = New-TemporaryMailCrypto -Smime
Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' `
    -Server 'smtp.example.com' -Port 25 -Subject 'Temporary cert test' `
    -Body 'Hello from temporary certificate' -Certificate $cert `
    -SignOrEncrypt SmimeSignAndEncrypt -WhatIf -Verbose
