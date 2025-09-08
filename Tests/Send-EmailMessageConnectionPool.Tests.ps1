Describe 'Send-EmailMessage connection pool' {
    It 'Reuses connection when pool enabled' {
        $refs = @(
            [Mailozaurr.ClientSmtp].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using MailKit.Security;
using System.Threading;
public class FakeClientPs : ClientSmtp {
    public int ConnectCalls;
    private bool _connected;
    public override bool IsConnected => _connected;
    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
        ConnectCalls++;
        _connected = true;
    }
    public override void Disconnect(bool quit, CancellationToken cancellationToken = default) {
        _connected = false;
    }
}
"@

        [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($true)
        [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
        $fake = [FakeClientPs]::new()
        [Mailozaurr.Smtp]::ClientFactory = { $fake }

        $params = @{ From = 'a@b.com'; To = 'c@d.com'; Server = 'h'; Subject = 't'; Text = 'b'; WhatIf = $true; UseConnectionPool = $true }
        Send-EmailMessage @params | Out-Null
        Send-EmailMessage @params | Out-Null

        $fake.ConnectCalls | Should -Be 1

        [Mailozaurr.Smtp]::ClientFactory = { [Mailozaurr.ClientSmtp]::new() }
        [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
        [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($false)
    }
}
