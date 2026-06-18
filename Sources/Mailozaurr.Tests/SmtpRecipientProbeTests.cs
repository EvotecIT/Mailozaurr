using MailKit.Security;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mailozaurr.Tests;

public class SmtpRecipientProbeTests {
    [Theory]
    [InlineData("example.com", "example-com.mail.protection.outlook.com")]
    [InlineData("Example.COM.", "example-com.mail.protection.outlook.com")]
    [InlineData("mail.example.com", "mail-example-com.mail.protection.outlook.com")]
    public void GetExchangeOnlineProtectionHost_BuildsDirectTarget(string domain, string expectedHost) {
        var host = Smtp.GetExchangeOnlineProtectionHost(domain);

        Assert.Equal(expectedHost, host);
    }

    [Theory]
    [InlineData("recipient@example.com\r\nDATA", "sender@example.com", "probe.local", "recipient must not contain CR or LF characters.")]
    [InlineData("recipient@example.com", "sender@example.com\r\nDATA", "probe.local", "sender must not contain CR or LF characters.")]
    [InlineData("recipient@example.com", "sender@example.com", "probe.local\r\nDATA", "heloHost must not contain CR or LF characters.")]
    public void TestRecipient_RejectsCommandInjectionInput(string recipient, string sender, string heloHost, string expectedError) {
        var result = Smtp.TestRecipient(
            "127.0.0.1",
            25,
            recipient,
            sender,
            heloHost,
            SecureSocketOptions.None);

        Assert.False(result.Accepted);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task TestRecipient_StopsBeforeDataAndReportsAccepted() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var commands = new List<string>();

            var serverTask = Task.Run(async () => {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
                using var writer = new StreamWriter(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: false) {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };

                await writer.WriteLineAsync("220 local.test ESMTP");
                while (true) {
                    var command = await reader.ReadLineAsync();
                    if (command == null) {
                        break;
                    }

                    commands.Add(command);
                    if (command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250-local.test");
                        await writer.WriteLineAsync("250 SIZE 1000000");
                    } else if (command.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.1.0 Sender OK");
                    } else if (command.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.1.5 Recipient OK");
                    } else if (command.Equals("RSET", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.0.0 Reset OK");
                    } else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("221 2.0.0 Bye");
                        break;
                    } else if (command.Equals("DATA", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("554 DATA is not expected in recipient probe");
                    }
                }
            });

            var result = Smtp.TestRecipient(
                "127.0.0.1",
                port,
                "recipient@example.com",
                "sender@example.com",
                "probe.local",
                SecureSocketOptions.None);

            await serverTask;

            Assert.True(result.Accepted);
            Assert.Equal(250, result.MailFromStatusCode);
            Assert.Equal(250, result.RecipientStatusCode);
            Assert.False(result.StartTlsUsed);
            Assert.Contains(commands, command => command.StartsWith("MAIL FROM:<sender@example.com>", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(commands, command => command.StartsWith("RCPT TO:<recipient@example.com>", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(commands, command => command.Equals("DATA", StringComparison.OrdinalIgnoreCase));
        } finally {
            listener.Stop();
        }
    }

    [Fact]
    public async Task TestRecipient_FallsBackToHeloWhenEhloIsRejected() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var commands = new List<string>();

            var serverTask = Task.Run(async () => {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
                using var writer = new StreamWriter(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: false) {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };

                await writer.WriteLineAsync("220 local.test SMTP");
                while (true) {
                    var command = await reader.ReadLineAsync();
                    if (command == null) {
                        break;
                    }

                    commands.Add(command);
                    if (command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("500 5.5.1 EHLO not supported");
                    } else if (command.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 local.test");
                    } else if (command.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.1.0 Sender OK");
                    } else if (command.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.1.5 Recipient OK");
                    } else if (command.Equals("RSET", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.0.0 Reset OK");
                    } else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("221 2.0.0 Bye");
                        break;
                    } else if (command.Equals("DATA", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("554 DATA is not expected in recipient probe");
                    }
                }
            });

            var result = Smtp.TestRecipient(
                "127.0.0.1",
                port,
                "recipient@example.com",
                "sender@example.com",
                "probe.local",
                SecureSocketOptions.None);

            await serverTask;

            Assert.True(result.Accepted);
            Assert.Equal(250, result.MailFromStatusCode);
            Assert.Equal(250, result.RecipientStatusCode);
            Assert.False(result.StartTlsUsed);
            Assert.Contains(commands, command => command.StartsWith("EHLO probe.local", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(commands, command => command.StartsWith("HELO probe.local", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(commands, command => command.Equals("DATA", StringComparison.OrdinalIgnoreCase));
        } finally {
            listener.Stop();
        }
    }

    [Fact]
    public async Task SendValidationMessage_SendsDataAndReturnsCorrelationFields() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var commands = new List<string>();
            var dataLines = new List<string>();

            var serverTask = Task.Run(async () => {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
                using var writer = new StreamWriter(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: false) {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };

                await writer.WriteLineAsync("220 local.test ESMTP");
                while (true) {
                    var command = await reader.ReadLineAsync();
                    if (command == null) {
                        break;
                    }

                    commands.Add(command);
                    if (command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250-local.test");
                        await writer.WriteLineAsync("250 SIZE 1000000");
                    } else if (command.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.1.0 Sender OK");
                    } else if (command.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("250 2.1.5 Recipient OK");
                    } else if (command.Equals("DATA", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                        while (true) {
                            var dataLine = await reader.ReadLineAsync();
                            if (dataLine == null || dataLine == ".") {
                                break;
                            }

                            dataLines.Add(dataLine);
                        }

                        await writer.WriteLineAsync("250 2.0.0 Queued");
                    } else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase)) {
                        await writer.WriteLineAsync("221 2.0.0 Bye");
                        break;
                    }
                }
            });

            var result = Smtp.SendValidationMessage(
                "127.0.0.1",
                port,
                new SmtpValidationMessageRequest {
                    Sender = "sender@example.com",
                    Recipient = "recipient@example.com",
                    HeloHost = "probe.local",
                    TestId = "DD-Test-123"
                },
                SecureSocketOptions.None);

            await serverTask;

            Assert.True(result.Sent);
            Assert.Equal("DD-Test-123", result.TestId);
            Assert.Equal("Authorized SMTP validation DD-Test-123", result.Subject);
            Assert.False(string.IsNullOrWhiteSpace(result.MessageId));
            Assert.Contains(commands, command => command.Equals("DATA", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(dataLines, line => line.Contains("X-Mailozaurr-Validation-TestId: DD-Test-123", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(dataLines, line => line.Contains("TestId: DD-Test-123", StringComparison.OrdinalIgnoreCase));
        } finally {
            listener.Stop();
        }
    }
}
