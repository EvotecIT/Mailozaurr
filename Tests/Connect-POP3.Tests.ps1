Describe 'Connect-POP3 SSL Options' {
    It 'Forwards provided enum to Pop3Connector' {
        $csc = Get-ChildItem "$HOME/.dotnet/sdk/*/Roslyn/bincore/csc.dll" | Sort-Object FullName -Descending | Select-Object -First 1
        $csPath = Join-Path $TestDrive 'FakePop3Client.cs'
        @"
using MailKit.Net.Pop3;
using MailKit.Security;
using System.Threading;
using System.Threading.Tasks;
public class FakePop3Client : Pop3Client {
    public SecureSocketOptions Passed;
    public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
        Passed = options;
        return Task.CompletedTask;
    }
    public override Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public override bool IsConnected => true;
    public override bool IsAuthenticated => true;
}
"@ | Set-Content $csPath
        $dllPath = Join-Path $TestDrive 'FakePop3Client.dll'
        $refs = @(
            "$HOME/.nuget/packages/mailkit/4.12.1/lib/netstandard2.0/MailKit.dll",
            "$HOME/.dotnet/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1/netstandard.dll"
        )
        $refArgs = $refs | ForEach-Object { "-r:" + $_ }
        & dotnet $csc.FullName $csPath -target:library -out:$dllPath $refArgs | Out-Null
        Add-Type -Path $dllPath

        $fake = [FakePop3Client]::new()
        [Mailozaurr.Pop3Connector]::ClientFactory = { $fake }

        $cred = [System.Management.Automation.PSCredential]::new('u',(ConvertTo-SecureString 'p' -AsPlainText -Force))
        Connect-POP3 -Server 'h' -Credential $cred -Port 995 -Options SslOnConnect | Out-Null

        $fake.Passed | Should -Be ([MailKit.Security.SecureSocketOptions]::SslOnConnect)

        [Mailozaurr.Pop3Connector]::ClientFactory = { [MailKit.Net.Pop3.Pop3Client]::new() }
    }

    It 'Uses StartTls when EnableExplicit set and Auto option' {
        $csc = Get-ChildItem "$HOME/.dotnet/sdk/*/Roslyn/bincore/csc.dll" | Sort-Object FullName -Descending | Select-Object -First 1
        $csPath = Join-Path $TestDrive 'FakePop3Client.cs'
        @"
using MailKit.Net.Pop3;
using MailKit.Security;
using System.Threading;
using System.Threading.Tasks;
public class FakePop3Client : Pop3Client {
    public SecureSocketOptions Passed;
    public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
        Passed = options;
        return Task.CompletedTask;
    }
    public override Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public override bool IsConnected => true;
    public override bool IsAuthenticated => true;
}
"@ | Set-Content $csPath
        $dllPath = Join-Path $TestDrive 'FakePop3Client.dll'
        $refs = @(
            "$HOME/.nuget/packages/mailkit/4.12.1/lib/netstandard2.0/MailKit.dll",
            "$HOME/.dotnet/packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1/netstandard.dll"
        )
        $refArgs = $refs | ForEach-Object { "-r:" + $_ }
        & dotnet $csc.FullName $csPath -target:library -out:$dllPath $refArgs | Out-Null
        Add-Type -Path $dllPath

        $fake = [FakePop3Client]::new()
        [Mailozaurr.Pop3Connector]::ClientFactory = { $fake }

        $cred = [System.Management.Automation.PSCredential]::new('u',(ConvertTo-SecureString 'p' -AsPlainText -Force))
        Connect-POP3 -Server 'h' -Credential $cred -Port 995 -EnableExplicit | Out-Null

        $fake.Passed | Should -Be ([MailKit.Security.SecureSocketOptions]::StartTls)

        [Mailozaurr.Pop3Connector]::ClientFactory = { [MailKit.Net.Pop3.Pop3Client]::new() }
    }
}
