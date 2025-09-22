using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MailKit.Security;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAsyncWrappersTests
{
    private class FakeClient : ClientSmtp
    {
        public bool ConnectCalled;
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, System.Threading.CancellationToken cancellationToken = default)
        {
            ConnectCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ConnectAsync_InvokesClientConnectAsync()
    {
        var smtp = new Smtp();
        var fake = new FakeClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);

        var result = await smtp.ConnectAsync("host", 25);

        Assert.True(fake.ConnectCalled);
        Assert.True(result.Status);
    }

    [Fact]
    public async Task CreateMessageAsync_WithAttachmentDescriptors_AddsAttachmentParts()
    {
        var descriptors = new List<SmtpAttachmentDescriptor>
        {
            new SmtpAttachmentByteArrayDescriptor(new byte[] { 1, 2, 3 }, "data.bin")
        };

        var smtp = new Smtp
        {
            From = "sender@example.com",
            To = new object[] { "recipient@example.com" },
            Subject = "Attachment Test",
            TextBody = "Body",
            Attachments = descriptors?.Select(static descriptor => (object)descriptor).ToList() ?? new List<object>(),
        };

        await smtp.CreateMessageAsync();

        var multipart = Assert.IsType<Multipart>(smtp.Message.Body);
        var attachment = Assert.IsType<MimePart>(Assert.Single(multipart.OfType<MimePart>().Where(static part => part.IsAttachment)));

        Assert.Equal("data.bin", attachment.FileName);
    }

    [Fact]
    public async Task CreateMessageAsync_WithNullAttachmentDescriptors_UsesEmptyList()
    {
        List<SmtpAttachmentDescriptor>? descriptors = null;

        var smtp = new Smtp
        {
            From = "sender@example.com",
            To = new object[] { "recipient@example.com" },
            Subject = "Attachment Test",
            TextBody = "Body",
            Attachments = descriptors?.Select(static descriptor => (object)descriptor).ToList() ?? new List<object>(),
        };

        await smtp.CreateMessageAsync();

        Assert.IsNotType<Multipart>(smtp.Message.Body);
    }

}
