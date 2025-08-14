Describe 'EphemeralOpenPgpContext - in-memory import' {
    It 'Encrypts and decrypts using string keys' {
        $pair = [Mailozaurr.TemporaryPgpKeyPair]::Create('a@b.com')
        $ctx = [Mailozaurr.EphemeralOpenPgpContext]::new($pair.PassPhrase)
        $ctx.ImportKeys($pair.ExportPublicKey())
        $ctx.ImportKeys($pair.ExportPrivateKey())

        $smtp = [Mailozaurr.Smtp]::new()
        $smtp.From = 'a@b.com'
        $smtp.To = @('a@b.com')
        $smtp.Subject = 'test'
        $smtp.TextBody = 'secret'
        $smtp.CreateMessage()
        $keys = $ctx.GetPublicKeys($smtp.Message.To)
        $smtp.Message.Body = [MimeKit.Cryptography.MultipartEncrypted]::Encrypt($ctx, $keys, $smtp.Message.Body)

        $encrypted = [MimeKit.Cryptography.MultipartEncrypted]$smtp.Message.Body
        $decrypted = $encrypted.Decrypt($ctx)
        $reader = New-Object System.IO.StreamReader($decrypted.Content.Open())
        $reader.ReadToEnd() | Should -Be 'secret'

        $ctx.Dispose()
        $pair.Dispose()
    }
}
