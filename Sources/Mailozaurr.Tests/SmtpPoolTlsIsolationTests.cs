#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MailKit.Security;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public sealed class SmtpPoolTlsIsolationTests {
    [Fact]
    public async Task StrictCaller_RejectsUntrustedServerAfterPermissiveConnectionWasPooled() {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Schannel requires a non-ephemeral key handle. Importing does not install
        // the certificate in a store; disposal releases its temporary key container.
        using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = new List<Task>();
        var serverFailures = new List<string>();
        var accept = AcceptAsync();
        var originalFactory = Smtp.ClientFactory;
        SmtpConnectionPool.ClearConnectionPool();
        Smtp.ResetClientFactory();
        try {
            var permissive = new Smtp { UseConnectionPool = true, SkipCertificateValidation = true };
            var permissiveResult = await permissive.ConnectAsync("127.0.0.1", port, SecureSocketOptions.SslOnConnect, false, timeout.Token);
            Assert.True(permissiveResult.Status, permissiveResult.Error + "\nServer: " + string.Join("\n", serverFailures));
            permissive.Dispose();
            var strict = new Smtp { UseConnectionPool = true, SkipCertificateValidation = false };
            try {
                var result = await strict.ConnectAsync("127.0.0.1", port, SecureSocketOptions.SslOnConnect, false, timeout.Token);
                Assert.False(result.Status);
                Assert.False(strict.Client.IsConnected);
            } finally {
                strict.Dispose();
            }
        } finally {
            SmtpConnectionPool.ClearConnectionPool();
            Smtp.ClientFactory = originalFactory;
            timeout.Cancel();
            listener.Stop();
            await accept;
            await Task.WhenAll(served);
        }

        async Task AcceptAsync() {
            try {
                for (var index = 0; index < 2; index++) {
                    var connection = await listener.AcceptTcpClientAsync(timeout.Token);
                    served.Add(ServeAsync(connection));
                }
            } catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }

        async Task ServeAsync(TcpClient connection) {
            using (connection)
            using (var tls = new SslStream(connection.GetStream())) {
                try {
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, timeout.Token);
                    using var reader = new StreamReader(tls, Encoding.ASCII, false, 1024, true);
                    using var writer = new StreamWriter(tls, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };
                    await writer.WriteLineAsync("220 localhost audit SMTP");
                    while (await reader.ReadLineAsync(timeout.Token) is string command) {
                        if (command.StartsWith("EHLO", StringComparison.Ordinal)) await writer.WriteLineAsync("250 localhost");
                        else if (command == "QUIT") { await writer.WriteLineAsync("221 Bye"); break; }
                        else await writer.WriteLineAsync("250 OK");
                    }
                } catch (System.Security.Authentication.AuthenticationException exception) { // Expected when strict validation rejects the self-signed server.
                    serverFailures.Add(exception.ToString());
                } catch (IOException exception) { // The client may close immediately after rejecting the handshake.
                    serverFailures.Add(exception.ToString());
                } catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
            }
        }
    }
}
#endif
