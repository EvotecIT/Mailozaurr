Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Generate temporary keys
$pair = [Mailozaurr.TemporaryPgpKeyPair]::Create('sender@example.com')
$public = $pair.ExportPublicKey()
$private = $pair.ExportPrivateKey()

# Create context and import keys entirely from memory
$ctx = [Mailozaurr.EphemeralOpenPgpContext]::new($pair.PassPhrase)
$ctx.ImportKeys($public)
$ctx.ImportKeys($private)

# Build a message and encrypt it using the imported keys
$smtp = [Mailozaurr.Smtp]::new()
$smtp.From = 'sender@example.com'
$smtp.To   = @('sender@example.com')
$smtp.Subject = 'Temporary in-memory PGP'
$smtp.TextBody = 'Secret text'
$smtp.CreateMessage()
$keys = $ctx.GetPublicKeys($smtp.Message.To)
$smtp.Message.Body = [MimeKit.Cryptography.MultipartEncrypted]::Encrypt($ctx, $keys, $smtp.Message.Body)

# Decrypt the message using the same in-memory context
$encrypted = [MimeKit.Cryptography.MultipartEncrypted]$smtp.Message.Body
$decrypted = $encrypted.Decrypt($ctx)
$reader = [System.IO.StreamReader]::new($decrypted.Content.Open())
Write-Host "Decrypted body: $($reader.ReadToEnd())"
