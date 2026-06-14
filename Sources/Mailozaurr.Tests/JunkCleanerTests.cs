using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class JunkCleanerTests {
    [Fact]
    public async Task GetImapJunkAsync_SingleUseSkipEnumerables_AreEnumeratedOnce() {
        var keepUid = new UniqueId(1);
        var skipUid = new UniqueId(2);
        var keepMessage = CreateMessage("keep@example.com", "keep-recipient@example.com", "normal", "keep-id");
        var filteredMessage = CreateMessage("skip-from@example.com", "skip-to@example.com", "urgent subject", "skip-id");

        var folder = new Mock<IMailFolder>();
        folder.SetupGet(f => f.FullName).Returns("Junk");
        folder.SetupGet(f => f.IsOpen).Returns(true);
        folder.SetupGet(f => f.Access).Returns(FolderAccess.ReadOnly);
        folder.Setup(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId> { keepUid, skipUid });
        folder.Setup(f => f.GetMessageAsync(keepUid, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(keepMessage);
        folder.Setup(f => f.GetMessageAsync(skipUid, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(filteredMessage);

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(Mock.Of<IMailFolder>(f => f.FullName == "Inbox"));
        client.Setup(c => c.GetFolder("Junk", It.IsAny<CancellationToken>())).Returns(folder.Object);
        client.Setup(c => c.PersonalNamespaces).Returns(new FolderNamespaceCollection());

        var skipFrom = new SingleUseEnumerable<string>("skip-from@example.com");
        var skipTo = new SingleUseEnumerable<string>("skip-to@example.com");
        var skipSubject = new SingleUseEnumerable<string>("urgent");
        var skipMessageId = new SingleUseEnumerable<string>("skip-id");

        var results = new List<ImapEmailMessage>();
        await foreach (var message in JunkCleaner.GetImapJunkAsync(
            client.Object,
            folder: "Junk",
            skipFrom: skipFrom,
            skipTo: skipTo,
            skipSubjectContains: skipSubject,
            skipMessageId: skipMessageId)) {
            results.Add(message);
        }

        var remaining = Assert.Single(results);
        Assert.Equal(keepUid, remaining.Uid);
        Assert.Equal(1, skipFrom.EnumerationCount);
        Assert.Equal(1, skipTo.EnumerationCount);
        Assert.Equal(1, skipSubject.EnumerationCount);
        Assert.Equal(1, skipMessageId.EnumerationCount);
    }

    [Fact]
    public async Task GetImapJunkAsync_NormalizesAttachmentExtensionsBeforeFiltering() {
        var keepUid = new UniqueId(1);
        var skipUid = new UniqueId(2);
        var keepMessage = CreateMessage("keep@example.com", "keep-recipient@example.com", "normal", "keep-id");
        var filteredMessage = CreateMessage("sender@example.com", "recipient@example.com", "subject", "skip-id", "invoice.pdf");

        var folder = new Mock<IMailFolder>();
        folder.SetupGet(f => f.FullName).Returns("Junk");
        folder.SetupGet(f => f.IsOpen).Returns(true);
        folder.SetupGet(f => f.Access).Returns(FolderAccess.ReadOnly);
        folder.Setup(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId> { keepUid, skipUid });
        folder.Setup(f => f.GetMessageAsync(keepUid, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(keepMessage);
        folder.Setup(f => f.GetMessageAsync(skipUid, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(filteredMessage);

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(Mock.Of<IMailFolder>(f => f.FullName == "Inbox"));
        client.Setup(c => c.GetFolder("Junk", It.IsAny<CancellationToken>())).Returns(folder.Object);
        client.Setup(c => c.PersonalNamespaces).Returns(new FolderNamespaceCollection());

        var results = new List<ImapEmailMessage>();
        await foreach (var message in JunkCleaner.GetImapJunkAsync(
            client.Object,
            folder: "Junk",
            skipAttachmentExtension: new[] { ".pdf" })) {
            results.Add(message);
        }

        var remaining = Assert.Single(results);
        Assert.Equal(keepUid, remaining.Uid);
    }

    private static MimeMessage CreateMessage(
        string from,
        string to,
        string subject,
        string messageId,
        string? attachmentName = null) {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.MessageId = messageId;

        if (attachmentName == null) {
            message.Body = new TextPart("plain") { Text = "body" };
            return message;
        }

        var builder = new BodyBuilder { TextBody = "body" };
        builder.Attachments.Add(attachmentName, new byte[] { 1, 2, 3 });
        message.Body = builder.ToMessageBody();
        return message;
    }

    private sealed class SingleUseEnumerable<T> : IEnumerable<T> {
        private readonly IReadOnlyList<T> items;

        public SingleUseEnumerable(params T[] items) {
            this.items = items;
        }

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator() {
            EnumerationCount++;
            if (EnumerationCount > 1) {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}