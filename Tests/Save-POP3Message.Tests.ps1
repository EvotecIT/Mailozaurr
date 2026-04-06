Describe 'Save-POP3Message' {
    It 'Creates directory when saving message' {
        $refs = @(
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MimeKit.MimeMessage].Assembly.Location
        )
        if (-not ('FakePop3MessageClient' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using MailKit.Net.Pop3;
using MimeKit;
public class FakePop3MessageClient : Pop3Client {
    private readonly MimeMessage _message;
    public FakePop3MessageClient() {
        _message = new MimeMessage();
        _message.From.Add(new MailboxAddress("a", "a@b.com"));
        _message.To.Add(new MailboxAddress("b", "b@c.com"));
        _message.Subject = "test";
        _message.Body = new TextPart("plain") { Text = "body" };
    }
    public override int Count => 1;
    public override MimeMessage GetMessage(int index, System.Threading.CancellationToken cancellationToken = default, MailKit.ITransferProgress progress = null) {
        return _message;
    }
}
"@
        }

        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        $info.Data = [FakePop3MessageClient]::new()
        $filePath = Join-Path $TestDrive 'subdir/message.eml'
        Save-POP3Message -Client $info -Index 0 -Path $filePath
        Test-Path $filePath | Should -Be $true
    }

    It 'Cleans up temp file on conversion failure' {
        $refs = @(
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MimeKit.MimeMessage].Assembly.Location
        )
        if (-not ('FakePop3MessageClient' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using MailKit.Net.Pop3;
using MimeKit;
public class FakePop3MessageClient : Pop3Client {
    private readonly MimeMessage _message;
    public FakePop3MessageClient() {
        _message = new MimeMessage();
        _message.From.Add(new MailboxAddress("a", "a@b.com"));
        _message.To.Add(new MailboxAddress("b", "b@c.com"));
        _message.Subject = "test";
        _message.Body = new TextPart("plain") { Text = "body" };
    }
    public override int Count => 1;
    public override MimeMessage GetMessage(int index, System.Threading.CancellationToken cancellationToken = default, MailKit.ITransferProgress progress = null) {
        return _message;
    }
}
"@
        }

        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        $info.Data = [FakePop3MessageClient]::new()

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
