Import-Module $PSScriptRoot\..\PSMailKit.psd1 -Force

$Protocol = [MailKit.Net.Pop3.Pop3Client]::new()
$Protocol.Connect
<#
void Connect(string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
void Connect(System.Net.Sockets.Socket socket, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
void Connect(System.IO.Stream stream, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
void Connect(uri uri, System.Threading.CancellationToken cancellationToken)
void Connect(string host, int port, bool useSsl, System.Threading.CancellationToken cancellationToken)
void IMailService.Connect(string host, int port, bool useSsl, System.Threading.CancellationToken cancellationToken)
void IMailService.Connect(string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
void IMailService.Connect(System.Net.Sockets.Socket socket, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
void IMailService.Connect(System.IO.Stream stream, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
#>

$Protocol.Authenticate

<#
void Authenticate(MailKit.Security.SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken)
void Authenticate(System.Text.Encoding encoding, System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
void Authenticate(System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
void Authenticate(System.Text.Encoding encoding, string userName, string password, System.Threading.CancellationToken cancellationToken)
void Authenticate(string userName, string password, System.Threading.CancellationToken cancellationToken)
void IMailService.Authenticate(System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
void IMailService.Authenticate(System.Text.Encoding encoding, System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
void IMailService.Authenticate(System.Text.Encoding encoding, string userName, string password, System.Threading.CancellationToken cancellationToken)
void IMailService.Authenticate(string userName, string password, System.Threading.CancellationToken cancellationToken)
void IMailService.Authenticate(MailKit.Security.SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken)
#>

$Protocol.GetMessageSizes