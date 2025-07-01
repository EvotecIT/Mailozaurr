Describe 'Save-POP3Message' {
    It 'Creates directory when saving message' {
        $csc = Get-ChildItem "$HOME/.dotnet/sdk/*/Roslyn/bincore/csc.dll" | Sort-Object FullName -Descending | Select-Object -First 1
        $csPath = Join-Path $TestDrive 'FakePop3Client.cs'
        @"
using MailKit.Net.Pop3;
using MimeKit;
public class FakePop3Client : Pop3Client {
    private readonly MimeMessage _message;
    public FakePop3Client() {
        _message = new MimeMessage();
        _message.From.Add(new MailboxAddress("a", "a@b.com"));
        _message.To.Add(new MailboxAddress("b", "b@c.com"));
        _message.Subject = "test";
        _message.Body = new TextPart("plain") { Text = "body" };
    }
    public override int Count => 1;
    public override MimeMessage GetMessage(int index, System.Threading.CancellationToken cancellationToken = default, MailKit.ITransferProgress? progress = null) {
        return _message;
    }
}
"@ | Set-Content $csPath
        $dllPath = Join-Path $TestDrive 'FakePop3Client.dll'
        $refs = @(
            "$HOME/.nuget/packages/mailkit/4.12.1/lib/netstandard2.0/MailKit.dll",
            "$HOME/.nuget/packages/mimekit/4.12.0/lib/netstandard2.0/MimeKit.dll",
            "$HOME/.dotnet/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1/netstandard.dll"
        )
        $refArgs = $refs | ForEach-Object { "-r:" + $_ }
        & dotnet $csc.FullName $csPath -target:library -out:$dllPath $refArgs | Out-Null
        Add-Type -Path $dllPath

        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        $info.Data = [FakePop3Client]::new()
        $filePath = Join-Path $TestDrive 'subdir/message.eml'
        Save-POP3Message -Client $info -Index 0 -Path $filePath
        Test-Path $filePath | Should -Be $true
    }

    It 'Cleans up temp file on conversion failure' {
        $csc = Get-ChildItem "$HOME/.dotnet/sdk/*/Roslyn/bincore/csc.dll" | Sort-Object FullName -Descending | Select-Object -First 1
        $csPath = Join-Path $TestDrive 'FakePop3Client.cs'
        @"
using MailKit.Net.Pop3;
using MimeKit;
public class FakePop3Client : Pop3Client {
    private readonly MimeMessage _message;
    public FakePop3Client() {
        _message = new MimeMessage();
        _message.From.Add(new MailboxAddress("a", "a@b.com"));
        _message.To.Add(new MailboxAddress("b", "b@c.com"));
        _message.Subject = "test";
        _message.Body = new TextPart("plain") { Text = "body" };
    }
    public override int Count => 1;
    public override MimeMessage GetMessage(int index, System.Threading.CancellationToken cancellationToken = default, MailKit.ITransferProgress? progress = null) {
        return _message;
    }
}
"@ | Set-Content $csPath
        $dllPath = Join-Path $TestDrive 'FakePop3Client.dll'
        $refs = @(
            "$HOME/.nuget/packages/mailkit/4.12.1/lib/netstandard2.0/MailKit.dll",
            "$HOME/.nuget/packages/mimekit/4.12.0/lib/netstandard2.0/MimeKit.dll",
            "$HOME/.dotnet/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1/netstandard.dll"
        )
        $refArgs = $refs | ForEach-Object { "-r:" + $_ }
        & dotnet $csc.FullName $csPath -target:library -out:$dllPath $refArgs | Out-Null
        Add-Type -Path $dllPath

        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        $info.Data = [FakePop3Client]::new()

        $tempDir = Join-Path $TestDrive 'tmp'
        [System.IO.Directory]::CreateDirectory($tempDir) | Out-Null
        $oldTemp = $env:TEMP
        $oldTmp = $env:TMP
        $env:TEMP = $tempDir
        $env:TMP = $tempDir

        $target = '/sys/fail.msg'

        Save-POP3Message -Client $info -Index 0 -Path $target | Out-Null

        (Get-ChildItem $tempDir -Filter '*.eml') | Should -BeNullOrEmpty

        $env:TEMP = $oldTemp
        $env:TMP = $oldTmp
    }
}
