Describe 'Send-MailozaurrPendingMessage' {
    It 'Sends and clears pending messages' {
        $path = Join-Path $TestDrive 'pending'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)
        $msg = [MimeKit.MimeMessage]::new()
        $msg.From.Add([MimeKit.MailboxAddress]::Parse('a@example.com'))
        $msg.To.Add([MimeKit.MailboxAddress]::Parse('b@example.com'))
        $msg.Subject = 'Pending'
        $msg.Body = [MimeKit.TextPart]::new('plain')
        $ms = [System.IO.MemoryStream]::new()
        $msg.WriteTo($ms)
        $record = [Mailozaurr.PendingMessageRecord]::new()
        $record.MessageId = $msg.MessageId
        $record.MimeMessage = [Convert]::ToBase64String($ms.ToArray())
        $record.Timestamp = [DateTimeOffset]::UtcNow
        $record.NextAttemptAt = [DateTimeOffset]::UtcNow
        $record.Server = 's'
        $record.Port = 25
        $repo.SaveAsync($record).GetAwaiter().GetResult()

        $refs = @(
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MimeKit.MimeMessage].Assembly.Location,
            [MailKit.Net.Smtp.SmtpClient].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Threading;
using System.Threading.Tasks;
public class FakeSendClient : ClientSmtp {
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {}
    public override Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }
}
"@
        $fake = [FakeSendClient]::new()
        [Mailozaurr.Smtp]::ClientFactory = { $fake }
        Send-MailozaurrPendingMessage -PendingMessagesPath $path
        [Mailozaurr.Smtp]::ClientFactory = { [Mailozaurr.ClientSmtp]::new() }

        (Get-MailozaurrPendingMessage -PendingMessagesPath $path | Measure-Object).Count | Should -Be 0
    }
}
