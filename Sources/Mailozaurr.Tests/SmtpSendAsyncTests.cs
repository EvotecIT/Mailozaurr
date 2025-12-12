using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
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

    [Fact]
    public async Task SendAsync_InvokesClientSendAsync()
    {
        var smtp = new Smtp();
        var fake = new FakeClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.WebhookUrl = null;
        await smtp.CreateMessageAsync();

        var result = await smtp.SendAsync();

        Assert.True(fake.Called);
        Assert.True(result.Status);
    }

    [Fact]
    public async Task SendAsync_DryRun_SkipsClientSendAsync()
    {
        var smtp = new Smtp();
        var fake = new FakeClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.WebhookUrl = null;
        smtp.DryRun = true;
        await smtp.CreateMessageAsync();

        var result = await smtp.SendAsync();

        Assert.False(fake.Called);
        Assert.False(result.Status);
        Assert.Equal("Email not sent (WhatIf)", result.Error);
    }
}
