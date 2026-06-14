using MailKit;
using MailKit.Security;
using MimeKit;
using System.Net.Security;
using System.Text;

namespace Mailozaurr.Tests.SentMessages;

public sealed class SmtpPendingMessageSenderTests {
    private sealed class RecordingClient : ClientSmtp {
        public string? Host { get; private set; }
        public int Port { get; private set; }
        public SecureSocketOptions Options { get; private set; }
        public MimeMessage? SentMessage { get; private set; }
        public bool WasDisconnected { get; private set; }
        public bool WasConnected { get; private set; }

        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default) {
            Host = host;
            Port = port;
            Options = options;
            WasConnected = true;
            return Task.CompletedTask;
        }

        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            SentMessage = message;
            return Task.FromResult(message.MessageId ?? string.Empty);
        }

        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default) {
            WasDisconnected = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SendAsync_UsesClientFactoryToSendMessage() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "queued-message";
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms);
        var record = new PendingMessageRecord {
            MimeMessage = Convert.ToBase64String(ms.ToArray()),
            MessageId = message.MessageId ?? string.Empty,
            Server = "smtp.example.com",
            Port = 2525
        };

        var client = new RecordingClient();
        var sender = new SmtpPendingMessageSender(() => client);
        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal("smtp.example.com", client.Host);
        Assert.Equal(2525, client.Port);
        Assert.Equal(SecureSocketOptions.Auto, client.Options);
        Assert.NotNull(client.SentMessage);
        Assert.Equal("queued", client.SentMessage!.Subject);
    }

    [Fact]
    public async Task SendAsync_ReplaysStoredSecuritySettings() {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "queued";
        message.Body = new TextPart("plain") { Text = "body" };
        message.MessageId = "queued-message-security";
        using var ms = new MemoryStream();
        await message.WriteToAsync(ms);

        var record = new PendingMessageRecord {
            MimeMessage = Convert.ToBase64String(ms.ToArray()),
            MessageId = message.MessageId ?? string.Empty,
            Server = "smtp.secure.example.com",
            Port = 465
        };

        record.ProviderData["SecureSocketOptions"] = SecureSocketOptions.Auto.ToString();
        record.ProviderData["UseSsl"] = bool.TrueString;
        record.ProviderData["SkipCertificateValidation"] = bool.TrueString;
        record.ProviderData["CheckCertificateRevocation"] = bool.FalseString;
        record.ProviderData["TimeoutMilliseconds"] = "12345";

        var client = new RecordingClient();
        var sender = new SmtpPendingMessageSender(
            () => client,
            secureSocketOptions: SecureSocketOptions.SslOnConnect,
            useSsl: false,
            skipCertificateValidation: false,
            checkCertificateRevocation: true,
            timeout: 54321);

        await sender.SendAsync(record, CancellationToken.None);

        Assert.Equal("smtp.secure.example.com", client.Host);
        Assert.Equal(465, client.Port);
        Assert.Equal(SecureSocketOptions.StartTls, client.Options);
        Assert.Equal(12345, client.Timeout);
        Assert.False(client.CheckCertificateRevocation);

        var callback = client.ServerCertificateValidationCallback;
        Assert.NotNull(callback);
        Assert.True(callback!(new object(), null!, null!, SslPolicyErrors.None));
    }
}