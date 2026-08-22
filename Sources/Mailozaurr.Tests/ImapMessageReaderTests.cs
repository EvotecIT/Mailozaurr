using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class ImapMessageReaderTests {
    [Fact]
    public async Task ReadAsync_UsesResolvedFolderAndTruncatesBodies() {
        var uid = new UniqueId(42);
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.com"));
        message.Subject = "subject";
        message.Date = new DateTimeOffset(2026, 3, 17, 10, 0, 0, TimeSpan.Zero);
        var builder = new BodyBuilder {
            TextBody = "1234567890",
            HtmlBody = "<p>abcdefghij</p>"
        };
        builder.Attachments.Add("report.txt", new byte[] { 1, 2, 3 });
        message.Body = builder.ToMessageBody();
        var forwarded = new MessagePart {
            Message = new MimeMessage(),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) {
                FileName = "forwarded.eml"
            }
        };
        Assert.IsAssignableFrom<Multipart>(message.Body).Add(forwarded);

        var folder = new Mock<IMailFolder>();
        folder.SetupGet(f => f.FullName).Returns("Inbox/Sub");
        folder.SetupGet(f => f.IsOpen).Returns(true);
        folder.SetupGet(f => f.Access).Returns(FolderAccess.ReadOnly);
        folder.Setup(f => f.GetMessageAsync(uid, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(message);

        var personalRoot = new Mock<IMailFolder>();
        personalRoot.Setup(f => f.GetSubfolder("Sub", It.IsAny<CancellationToken>()))
            .Returns(folder.Object);

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(Mock.Of<IMailFolder>(f => f.FullName == "Inbox"));
        client.Setup(c => c.GetFolder("Sub", It.IsAny<CancellationToken>()))
            .Throws(new FolderNotFoundException("Sub"));
        client.Setup(c => c.GetFolder(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => name.Length == 0 ? personalRoot.Object : throw new FolderNotFoundException(name));

        var personalNamespaces = new FolderNamespaceCollection();
        personalNamespaces.Add(new FolderNamespace('.', ""));
        client.Setup(c => c.GetFolder(It.IsAny<FolderNamespace>()))
            .Returns(personalRoot.Object);
        client.Setup(c => c.PersonalNamespaces)
            .Returns(personalNamespaces);

        var result = await ImapMessageReader.ReadAsync(
            client.Object,
            new ImapMessageReadRequest(uid, "Sub", 8),
            CancellationToken.None);

        Assert.Equal(42L, result.Uid);
        Assert.Equal("Inbox/Sub", result.Folder);
        Assert.Equal("subject", result.Subject);
        Assert.True(result.TextTruncated);
        Assert.True(result.HtmlTruncated);
        Assert.True(result.HasAttachments);
        Assert.Collection(
            result.Attachments,
            attachment => Assert.Equal("report.txt", attachment.FileName),
            attachment => Assert.Equal("forwarded.eml", attachment.FileName));
    }

    [Fact]
    public async Task ReadAsync_UsesInboxWhenFolderOmitted() {
        var uid = new UniqueId(7);
        var message = new MimeMessage();
        message.Body = new TextPart("plain") { Text = "body" };

        var inbox = new Mock<IMailFolder>();
        inbox.SetupGet(f => f.FullName).Returns("Inbox");
        inbox.SetupGet(f => f.IsOpen).Returns(true);
        inbox.SetupGet(f => f.Access).Returns(FolderAccess.ReadOnly);
        inbox.Setup(f => f.GetMessageAsync(uid, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(message);

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(inbox.Object);
        client.Setup(c => c.PersonalNamespaces).Returns(new FolderNamespaceCollection());

        var result = await ImapMessageReader.ReadAsync(
            client.Object,
            new ImapMessageReadRequest(uid, null, 128),
            CancellationToken.None);

        Assert.Equal("Inbox", result.Folder);
        Assert.False(result.TextTruncated);
        Assert.False(result.HasAttachments);
    }
}
