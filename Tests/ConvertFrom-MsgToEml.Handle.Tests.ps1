Describe 'ConvertFrom-MsgToEml handle management' {
    It 'Releases file handle after conversion' {
        $temp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
        New-Item -ItemType Directory -Path $temp | Out-Null
        $eml = Join-Path $temp 'mail.eml'
        $msg = Join-Path $temp 'mail.msg'
        $out = Join-Path $temp 'out'
        New-Item -ItemType Directory -Path $out | Out-Null

        $message = [MimeKit.MimeMessage]::new()
        $message.Subject = 'test'
        $message.Body = [MimeKit.TextPart]::new('plain', 'body')
        $message.WriteTo($eml)

        [Mailozaurr.EmailMessage]::ConvertEmlToMsg([System.IO.FileInfo]$eml, [System.IO.FileInfo]$msg, $true) | Out-Null

        ConvertFrom-MsgToEml -InputPath $msg -OutputFolder $out -Force

        Remove-Item $msg
        Test-Path $msg | Should -BeFalse

        Remove-Item -Recurse -Force $temp
    }
}
