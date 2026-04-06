Describe 'Connect-POP3 SSL Options' {
    It 'Forwards provided enum to Pop3Connector' {
        $refs = @(
            [Mailozaurr.Pop3Connector].Assembly.Location,
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        if (-not ('FakePop3Client' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
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
    public override bool IsConnected => true;
    public override bool IsAuthenticated => true;
}
public static class FakePop3ClientFactory {
    public static Pop3Client Client;
    public static Pop3Client Create() => Client;
    public static void Install() => Pop3Connector.ClientFactory = Create;
}
"@
        }

        $fake = [FakePop3Client]::new()
        [FakePop3ClientFactory]::Client = $fake
        [FakePop3ClientFactory]::Install()

        Connect-POP3 -Server 'h' -UserName 'u' -Password 'p' -Port 995 -Options SslOnConnect | Out-Null

        $fake.Passed | Should -Be ([MailKit.Security.SecureSocketOptions]::SslOnConnect)

        [Mailozaurr.Pop3Connector]::ResetClientFactory()
    }

    It 'Uses StartTls when EnableExplicit set and Auto option' {
        $refs = @(
            [Mailozaurr.Pop3Connector].Assembly.Location,
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        if (-not ('FakePop3Client' -as [type])) {
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
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
    public override bool IsConnected => true;
    public override bool IsAuthenticated => true;
}
public static class FakePop3ClientFactory {
    public static Pop3Client Client;
    public static Pop3Client Create() => Client;
    public static void Install() => Pop3Connector.ClientFactory = Create;
}
"@
        }

        $fake = [FakePop3Client]::new()
        [FakePop3ClientFactory]::Client = $fake
        [FakePop3ClientFactory]::Install()

        Connect-POP3 -Server 'h' -UserName 'u' -Password 'p' -Port 995 -EnableExplicit | Out-Null

        $fake.Passed | Should -Be ([MailKit.Security.SecureSocketOptions]::StartTls)

        [Mailozaurr.Pop3Connector]::ResetClientFactory()
    }
}
