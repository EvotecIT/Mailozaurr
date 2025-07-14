Describe 'Clear-SmtpConnectionPool' {
    It 'Forces new connection after clearing pool' {
        Add-Type -TypeDefinition @"
using Mailozaurr;
using MailKit.Security;
using System.Threading;
public class FakeClientPs2 : ClientSmtp {
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

        [Mailozaurr.Smtp]::PoolingEnabled = $true
        [Mailozaurr.Smtp]::ClearConnectionPool()
        $fake = [FakeClientPs2]::new()
        [Mailozaurr.Smtp]::ClientFactory = { $fake }

        $params = @{ From='a@b.com'; To='c@d.com'; Server='h'; Subject='t'; Text='b'; WhatIf=$true; UseConnectionPool=$true }
        Send-EmailMessage @params | Out-Null
        Send-EmailMessage @params | Out-Null

        Clear-SmtpConnectionPool

        Send-EmailMessage @params | Out-Null

        $fake.ConnectCalls | Should -Be 2

        [Mailozaurr.Smtp]::ClientFactory = { [Mailozaurr.ClientSmtp]::new() }
        [Mailozaurr.Smtp]::ClearConnectionPool()
        [Mailozaurr.Smtp]::PoolingEnabled = $false
    }
}
