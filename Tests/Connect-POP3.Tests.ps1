Describe 'Connect-POP3 SSL Options' {
    It 'Forwards provided enum to Pop3Connector' {
        $refs = @(
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
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
"@

        $fake = [FakePop3Client]::new()
        [Mailozaurr.Pop3Connector]::ClientFactory = { $fake }

        $cred = [System.Management.Automation.PSCredential]::new('u',(ConvertTo-SecureString 'p' -AsPlainText -Force))
        Connect-POP3 -Server 'h' -Credential $cred -Port 995 -Options SslOnConnect | Out-Null

        $fake.Passed | Should -Be ([MailKit.Security.SecureSocketOptions]::SslOnConnect)

        [Mailozaurr.Pop3Connector]::ClientFactory = { [MailKit.Net.Pop3.Pop3Client]::new() }
    }

    It 'Uses StartTls when EnableExplicit set and Auto option' {
        $refs = @(
            [MailKit.Net.Pop3.Pop3Client].Assembly.Location,
            [MailKit.Security.SecureSocketOptions].Assembly.Location
        )
        Add-Type -ReferencedAssemblies $refs -TypeDefinition @"
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
"@

        $fake = [FakePop3Client]::new()
        [Mailozaurr.Pop3Connector]::ClientFactory = { $fake }

        $cred = [System.Management.Automation.PSCredential]::new('u',(ConvertTo-SecureString 'p' -AsPlainText -Force))
        Connect-POP3 -Server 'h' -Credential $cred -Port 995 -EnableExplicit | Out-Null

        $fake.Passed | Should -Be ([MailKit.Security.SecureSocketOptions]::StartTls)

        [Mailozaurr.Pop3Connector]::ClientFactory = { [MailKit.Net.Pop3.Pop3Client]::new() }
    }
}
