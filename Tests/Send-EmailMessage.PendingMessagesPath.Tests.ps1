Describe 'Send-EmailMessage PendingMessagesPath' {
    It 'Queues message when send fails' {
        $path = Join-Path $TestDrive 'pending.log'
        $refs = @(
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MimeKit.MimeMessage].Assembly.Location,
            [MailKit.Net.Smtp.SmtpClient].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
using Mailozaurr;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Threading;
using System.Threading.Tasks;
public class FailClient : ClientSmtp {
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {}
    public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
        throw new System.Exception("fail");
    }
}
"@
        $fake = [FailClient]::new()
        [Mailozaurr.Smtp]::ClientFactory = { $fake }
        try {
            Send-EmailMessage -From 'a@b.com' -To 'b@c.com' -Server 's' -Subject 't' -Text 'b' -PendingMessagesPath $path -ErrorAction SilentlyContinue | Out-Null
            (Get-MailozaurrPendingMessage -PendingMessagesPath $path | Measure-Object).Count | Should -Be 1
        } finally {
            [Mailozaurr.Smtp]::ClientFactory = { [Mailozaurr.ClientSmtp]::new() }
        }
    }
}
