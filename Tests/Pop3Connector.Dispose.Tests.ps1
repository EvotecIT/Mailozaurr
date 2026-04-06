Describe 'Pop3Connector disposal' {
    BeforeAll {
        $refs = @(
            [Mailozaurr.Pop3Connector].Assembly.Location,
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
    }

    It 'Disposes client after failed connect' {
        if (-not ('FailingPop3Client' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
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
public static class FailingPop3ClientFactory {
    public static Pop3Client Client;
    public static Pop3Client Create() => Client;
    public static void Install() => Pop3Connector.ClientFactory = Create;
}
"@
        }

        $fake = [FailingPop3Client]::new()
        [FailingPop3ClientFactory]::Client = $fake
        [FailingPop3ClientFactory]::Install()

        try {
            [Mailozaurr.Pop3Connector]::ConnectAsync('h', 995, [MailKit.Security.SecureSocketOptions]::SslOnConnect, 1000, $false, $false, { param($c) [Task]::CompletedTask }, 0, 0, 1.0).GetAwaiter().GetResult()
        } catch {
        }

        $fake.Disposed | Should -BeTrue

        [Mailozaurr.Pop3Connector]::ResetClientFactory()
    }

    It 'Disposes client even when DisconnectAsync throws' {
        if (-not ('ThrowingDisconnectPop3Client' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
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
public static class ThrowingDisconnectPop3ClientFactory {
    public static Pop3Client Client;
    public static Pop3Client Create() => Client;
    public static void Install() => Pop3Connector.ClientFactory = Create;
}
"@
        }

        $fake = [ThrowingDisconnectPop3Client]::new()
        [ThrowingDisconnectPop3ClientFactory]::Client = $fake
        [ThrowingDisconnectPop3ClientFactory]::Install()

        try {
            [Mailozaurr.Pop3Connector]::ConnectAsync('h', 995, [MailKit.Security.SecureSocketOptions]::SslOnConnect, 1000, $false, $false, { param($c) [Task]::CompletedTask }, 0, 0, 1.0).GetAwaiter().GetResult()
        } catch {
        }

        $fake.DisconnectCalled | Should -BeTrue
        $fake.Disposed | Should -BeTrue

        [Mailozaurr.Pop3Connector]::ResetClientFactory()
    }
}
