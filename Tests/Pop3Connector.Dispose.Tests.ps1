Describe 'Pop3Connector disposal' {
    BeforeAll {
        $refs = @(
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
    }

    It 'Disposes client after failed connect' {
        Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
using MailKit.Net.Pop3;
using MailKit.Security;
using System.Threading;
using System.Threading.Tasks;
public class FailingPop3Client : Pop3Client {
    public bool Disposed;
    public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
        throw new System.InvalidOperationException("fail");
    }
    public override bool IsConnected => true;
    public override bool IsAuthenticated => false;
    protected override void Dispose(bool disposing) {
        Disposed = true;
        base.Dispose(disposing);
    }
}
"@

        $fake = [FailingPop3Client]::new()
        [Mailozaurr.Pop3Connector]::ClientFactory = { $fake }

        try {
            [Mailozaurr.Pop3Connector]::ConnectAsync('h', 995, [MailKit.Security.SecureSocketOptions]::SslOnConnect, 1000, $false, $false, { param($c) [Task]::CompletedTask }, 0, 0, 1.0).GetAwaiter().GetResult()
        } catch {
        }

        $fake.Disposed | Should -BeTrue

        [Mailozaurr.Pop3Connector]::ClientFactory = { [MailKit.Net.Pop3.Pop3Client]::new() }
    }

    It 'Disposes client even when DisconnectAsync throws' {
        Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
using MailKit.Net.Pop3;
using MailKit.Security;
using System.Threading;
using System.Threading.Tasks;
public class ThrowingDisconnectPop3Client : Pop3Client {
    public bool Disposed;
    public bool DisconnectCalled;
    public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
        throw new System.InvalidOperationException("fail");
    }
    public override bool IsConnected => true;
    public override bool IsAuthenticated => false;
    public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default) {
        DisconnectCalled = true;
        throw new System.InvalidOperationException("disconnect");
    }
    protected override void Dispose(bool disposing) {
        Disposed = true;
        base.Dispose(disposing);
    }
}
"@

        $fake = [ThrowingDisconnectPop3Client]::new()
        [Mailozaurr.Pop3Connector]::ClientFactory = { $fake }

        try {
            [Mailozaurr.Pop3Connector]::ConnectAsync('h', 995, [MailKit.Security.SecureSocketOptions]::SslOnConnect, 1000, $false, $false, { param($c) [Task]::CompletedTask }, 0, 0, 1.0).GetAwaiter().GetResult()
        } catch {
        }

        $fake.DisconnectCalled | Should -BeTrue
        $fake.Disposed | Should -BeTrue

        [Mailozaurr.Pop3Connector]::ClientFactory = { [MailKit.Net.Pop3.Pop3Client]::new() }
    }
}

