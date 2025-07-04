using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpSendAsyncTests
{
    private class FakeClient : ClientSmtp
    {
        public bool Called;
        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
        {
            Called = true;
            return Task.FromResult(string.Empty);
        }
    }

    private static void SetClient(Smtp smtp, ClientSmtp client)
    {
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, client);
    }

    [Fact]
    public async Task SendAsync_InvokesClientSendAsync()
    {
        var smtp = new Smtp();
        var fake = new FakeClient();
        SetClient(smtp, fake);
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.WebhookUrl = null;
        smtp.CreateMessage();

        var result = await smtp.SendAsync();

        Assert.True(fake.Called);
        Assert.True(result.Status);
    }

    private class FakeConnectClient : ClientSmtp
    {
        public SecureSocketOptions? Options;
        public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            Options = options;
        }
    }

    [Fact]
    public void Connect_UseSslAuto_UsesStartTls()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);

        smtp.Connect("server", 587, SecureSocketOptions.Auto, true);

        Assert.Equal(SecureSocketOptions.StartTls, fake.Options);
    }

    private class RetryClient : ClientSmtp
    {
        private readonly int _failures;
        public int SendCount;
        public List<DateTimeOffset> CallTimes { get; } = new();

        public RetryClient(int failures)
        {
            _failures = failures;
        }

        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
        {
            SendCount++;
            CallTimes.Add(DateTimeOffset.UtcNow);
            if (SendCount <= _failures)
                throw new SmtpProtocolException("fail");
            return Task.FromResult(string.Empty);
        }
    }

    [Fact]
    public async Task SendCoreAsync_Retries_WithDelayAndBackoff()
    {
        var smtp = new Smtp();
        var fake = new RetryClient(2);
        SetClient(smtp, fake);
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.WebhookUrl = null;
        smtp.CreateMessage();

        smtp.RetryCount = 2;
        smtp.RetryDelayMilliseconds = 20;
        smtp.RetryDelayBackoff = 2.0;
        smtp.RetryAlways = true;

        var result = await smtp.SendAsync();

        Assert.True(result.Status);
        Assert.Equal(3, fake.SendCount);
        Assert.True(fake.CallTimes[1] - fake.CallTimes[0] >= TimeSpan.FromMilliseconds(20));
        Assert.True(fake.CallTimes[2] - fake.CallTimes[1] >= TimeSpan.FromMilliseconds(40));
    }
}
