# PGP Support in Mailozaurr

Mailozaurr is able to sign and encrypt messages using OpenPGP. The implementation relies on [`MimeKit`](https://github.com/jstedfast/MimeKit) and a temporary key store so that no permanent changes are made to the user's environment.

## Ephemeral key store

The `EphemeralOpenPgpContext` class creates a temporary directory that is removed when the context is disposed. It inherits from `GnuPGContext` and is used internally by the SMTP helper methods when signing or encrypting with PGP.

```csharp
using var ctx = new EphemeralOpenPgpContext();
// import keys and use ctx for signing or encryption
```

## Sending a PGP encrypted message

Use `Send-EmailMessage` with the `PgpSignAndEncrypt` option. Provide paths to the public and private key files and the password protecting the private key:

```powershell
$pub = 'Examples/PGPKeys/mimekit.gpg.pub'
$sec = 'Examples/PGPKeys/mimekit.gpg.sec'
Send-EmailMessage -From 'mimekit@example.com' -To 'mimekit@example.com' \ 
    -Server 'smtp.example.com' -Port 25 -Body 'Test' -Subject 'Test' \ 
    -SignOrEncrypt PgpSignAndEncrypt -PublicKeyPath $pub -PrivateKeyPath $sec \ 
    -PrivateKeyPassword 'no.secret'
```

The message will be signed and encrypted before being sent.
