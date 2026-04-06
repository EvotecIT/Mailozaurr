Describe 'Send-EmailMessage connection pool' {
    It 'Reuses connection when pool enabled' {
        $refs = @(
            [Mailozaurr.Smtp].Assembly.Location,
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MimeKit.MimeMessage].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        if (-not ('FakeClientPsPool' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using MailKit;
using MailKit.Security;
using MimeKit;
using System.Threading;
using System.Threading.Tasks;
public class FakeClientPs : ClientSmtp {
    public static int ConnectCalls;
    private bool _connected;
    public override bool IsConnected => _connected;
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
        ConnectCalls++;
        _connected = true;
    }
    public override void Disconnect(bool quit, CancellationToken cancellationToken = default) {
        _connected = false;
    }
    public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken, ITransferProgress progress) {
        message.MessageId = message.MessageId ?? "fake-" + ConnectCalls.ToString();
        return Task.FromResult(message.MessageId);
    }
}
public static class FakeClientPsPool {
    public static ClientSmtp Create(ProtocolLogger logger) => new FakeClientPs();
    public static void Install() => Smtp.ClientFactory = Create;
    public static void Reset() => FakeClientPs.ConnectCalls = 0;
}
"@
        }

        try {
            [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($false)
            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            [FakeClientPsPool]::Reset()
            [FakeClientPsPool]::Install()

            $params = @{ From = 'a@b.com'; To = 'c@d.com'; Server = 'h'; Subject = 't'; Text = 'b'; UseConnectionPool = $true }
            Send-EmailMessage @params | Out-Null
            Send-EmailMessage @params | Out-Null

            [FakeClientPs]::ConnectCalls | Should -Be 1
        } finally {
            [Mailozaurr.Smtp]::ResetClientFactory()
            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($false)
        }
    }
}
