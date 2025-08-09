Describe 'Pop3Connector disposal' {
    BeforeAll {
        $csc = Get-ChildItem "$HOME/.dotnet/sdk/*/Roslyn/bincore/csc.dll" | Sort-Object FullName -Descending | Select-Object -First 1
        $refs = @(
            "$HOME/.nuget/packages/mailkit/4.12.1/lib/netstandard2.0/MailKit.dll",
            "$HOME/.dotnet/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1/netstandard.dll"
        )
        $refArgs = $refs | ForEach-Object { "-r:" + $_ }
    }

    It 'Disposes client after failed connect' {
        $csPath = Join-Path $TestDrive 'FailingPop3Client.cs'
        @"
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
"@ | Set-Content $csPath
        $dllPath = Join-Path $TestDrive 'FailingPop3Client.dll'
        & dotnet $csc.FullName $csPath -target:library -out:$dllPath $refArgs | Out-Null
        Add-Type -Path $dllPath

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
        $csPath = Join-Path $TestDrive 'ThrowingDisconnectPop3Client.cs'
        @"
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
"@ | Set-Content $csPath
        $dllPath = Join-Path $TestDrive 'ThrowingDisconnectPop3Client.dll'
        & dotnet $csc.FullName $csPath -target:library -out:$dllPath $refArgs | Out-Null
        Add-Type -Path $dllPath

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

