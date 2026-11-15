BeforeAll { . (Join-Path $PSScriptRoot 'TestHelpers/Add-MailozaurrTestType.ps1') }

Describe 'Clear-SmtpConnectionPool' {
    It 'Forces new connection after clearing pool' {
        $refs = @(
            [Mailozaurr.Smtp].Assembly.Location,
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MimeKit.MimeMessage].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        if (-not ('FakeClientPsClearPool' -as [type])) {
            Add-MailozaurrTestType -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using MailKit;
using MailKit.Security;
using MimeKit;
using System.Threading;
using System.Threading.Tasks;
public class FakeClientPs2 : ClientSmtp {
    public static int ConnectCalls;
    private bool _connected;
    public override bool IsConnected { get { return _connected; } }
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default(CancellationToken)) {
        ConnectCalls++;
        _connected = true;
    }
    public override void Disconnect(bool quit, CancellationToken cancellationToken = default(CancellationToken)) {
        _connected = false;
    }
    public override Task<MailKit.SendResult> SendAsync(FormatOptions options, ISendRequest request, CancellationToken cancellationToken = default(CancellationToken)) {
        var message = request.Message;
        message.MessageId = message.MessageId ?? "fake-" + ConnectCalls.ToString();
        return Task.FromResult(new MailKit.SendResult(message.MessageId, new System.Collections.Generic.List<MailboxAddress>(message.To.Mailboxes), new MailboxAddress[0]));
    }
}
public static class FakeClientPsClearPool {
    public static ClientSmtp Create(ProtocolLogger logger) { return new FakeClientPs2(); }
    public static void Install() { Smtp.ClientFactory = Create; }
    public static void Reset() { FakeClientPs2.ConnectCalls = 0; }
}
"@
        }

        try {
            [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($false)
            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            [FakeClientPsClearPool]::Reset()
            [FakeClientPsClearPool]::Install()

            $params = @{ From='a@b.com'; To='c@d.com'; Server='h'; Subject='t'; Text='b'; UseConnectionPool=$true }
            Send-EmailMessage @params | Out-Null
            Send-EmailMessage @params | Out-Null

            Clear-SmtpConnectionPool

            Send-EmailMessage @params | Out-Null

            [FakeClientPs2]::ConnectCalls | Should -Be 2
        } finally {
            [Mailozaurr.Smtp]::ResetClientFactory()
            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($false)
        }
    }
}
