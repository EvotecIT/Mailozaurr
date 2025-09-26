using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;
using Mailozaurr.Definitions;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAsyncWrappersTests
{
    private class FakeConnectClient : ClientSmtp
    {
        public bool ConnectCalled;
        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            ConnectCalled = true;
            return Task.CompletedTask;
        }
    }

    private static void SetClient(Smtp smtp, ClientSmtp client)
    {
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, client);
    }

    [Fact]
    public async Task ConnectAsync_InvokesClientConnectAsync()
    {
        var smtp = new Smtp();
        var fake = new FakeConnectClient();
        SetClient(smtp, fake);

        var result = await smtp.ConnectAsync("host", 25);

        Assert.True(fake.ConnectCalled);
        Assert.True(result.Status);
    }

    [Fact]
    public async Task CreateMessageAsync_AutoEmbedImagesAddsInlineAttachment()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "data");

            var smtp = new Smtp
            {
                AutoEmbedImages = true,
                HtmlBody = $"<img src=\"{tempFile}\">",
                From = "sender@example.com",
                To = new object[] { "recipient@example.com" },
                Subject = "test",
                TextBody = "body"
            };

            await smtp.CreateMessageAsync();

            var inlineAttachments = smtp.InlineAttachments ?? new List<AttachmentDescriptor>();
            Assert.Contains(inlineAttachments.OfType<FileAttachmentDescriptor>(), a => string.Equals(a.FilePath, tempFile, StringComparison.OrdinalIgnoreCase));
            Assert.Contains($"cid:{Path.GetFileName(tempFile)}", smtp.HtmlBody);
            Assert.Contains($"cid:{Path.GetFileName(tempFile)}", smtp.Message.HtmlBody);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task CreateMessageAsync_CancellationTokenPreventsClientInvocation()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "data");

            var smtp = new Smtp
            {
                AutoEmbedImages = true,
                HtmlBody = $"<img src=\"{tempFile}\">"
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var originalHtml = smtp.HtmlBody;

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await smtp.CreateMessageAsync(cts.Token));

            Assert.Equal(originalHtml, smtp.HtmlBody);
            var inlineAttachments = smtp.InlineAttachments ?? new List<AttachmentDescriptor>();
            Assert.DoesNotContain(inlineAttachments.OfType<FileAttachmentDescriptor>(), a => string.Equals(a.FilePath, tempFile, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

}
