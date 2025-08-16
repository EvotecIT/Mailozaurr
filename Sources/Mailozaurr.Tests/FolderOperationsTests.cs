using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using Moq;
using Xunit;

namespace Mailozaurr.Tests;

public class FolderOperationsTests {
    [Fact]
    public async Task MoveFolderAsync_ClosesFolders() {
        var source = new Mock<IMailFolder>();
        var dest = new Mock<IMailFolder>();

        source.SetupGet(f => f.FullName).Returns("source");
        source.SetupGet(f => f.Name).Returns("source");
        source.SetupGet(f => f.IsOpen).Returns(true);
        source.Setup(f => f.Open(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
        source.Setup(f => f.RenameAsync(dest.Object, "source", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        source.Setup(f => f.CloseAsync(false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        dest.SetupGet(f => f.FullName).Returns("dest");
        dest.SetupGet(f => f.IsOpen).Returns(true);
        dest.Setup(f => f.Open(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
        dest.Setup(f => f.CloseAsync(false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(source.Object);
        client.Setup(c => c.GetFolder(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(dest.Object);
        client.Setup(c => c.PersonalNamespaces).Returns(new FolderNamespaceCollection());

        await FolderOperations.MoveFolderAsync(client.Object, "source", "dest");

        source.Verify(f => f.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once());
        dest.Verify(f => f.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task MoveFolderAsync_ClosesFolders_OnFailure() {
        var source = new Mock<IMailFolder>();
        var dest = new Mock<IMailFolder>();

        source.SetupGet(f => f.FullName).Returns("source");
        source.SetupGet(f => f.Name).Returns("source");
        source.SetupGet(f => f.IsOpen).Returns(true);
        source.Setup(f => f.Open(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
        source.Setup(f => f.RenameAsync(dest.Object, "source", It.IsAny<CancellationToken>())).ThrowsAsync(new ImapProtocolException("fail"));
        source.Setup(f => f.CloseAsync(false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        dest.SetupGet(f => f.FullName).Returns("dest");
        dest.SetupGet(f => f.IsOpen).Returns(true);
        dest.Setup(f => f.Open(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()));
        dest.Setup(f => f.CloseAsync(false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(source.Object);
        client.Setup(c => c.GetFolder(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(dest.Object);
        client.Setup(c => c.PersonalNamespaces).Returns(new FolderNamespaceCollection());

        await Assert.ThrowsAsync<ImapProtocolException>(() => FolderOperations.MoveFolderAsync(client.Object, "source", "dest"));

        source.Verify(f => f.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once());
        dest.Verify(f => f.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RenameFolderAsync_ClosesFolder_OnFailure() {
        var folder = new Mock<IMailFolder>();

        folder.SetupGet(f => f.FullName).Returns("source");
        folder.SetupGet(f => f.ParentFolder).Returns(Mock.Of<IMailFolder>());
        folder.SetupGet(f => f.IsOpen).Returns(true);
        folder.Setup(f => f.RenameAsync(It.IsAny<IMailFolder>(), "new", It.IsAny<CancellationToken>())).ThrowsAsync(new ImapProtocolException("fail"));
        folder.Setup(f => f.CloseAsync(false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(Mock.Of<IMailFolder>(f => f.FullName == "inbox"));
        client.Setup(c => c.GetFolder("source", It.IsAny<CancellationToken>())).Returns(folder.Object);
        client.Setup(c => c.PersonalNamespaces).Returns(new FolderNamespaceCollection());

        await Assert.ThrowsAsync<ImapProtocolException>(() => FolderOperations.RenameFolderAsync(client.Object, "source", "new"));

        folder.Verify(f => f.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RemoveFolderAsync_ClosesFolder_OnFailure() {
        var folder = new Mock<IMailFolder>();

        folder.SetupGet(f => f.FullName).Returns("source");
        folder.SetupGet(f => f.IsOpen).Returns(true);
        folder.Setup(f => f.DeleteAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new ImapProtocolException("fail"));
        folder.Setup(f => f.CloseAsync(false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var client = new Mock<ImapClient> { CallBase = true };
        client.Setup(c => c.Inbox).Returns(Mock.Of<IMailFolder>(f => f.FullName == "inbox"));
        client.Setup(c => c.GetFolder("source", It.IsAny<CancellationToken>())).Returns(folder.Object);
        client.Setup(c => c.PersonalNamespaces).Returns(new FolderNamespaceCollection());

        await Assert.ThrowsAsync<ImapProtocolException>(() => FolderOperations.RemoveFolderAsync(client.Object, "source"));

        folder.Verify(f => f.CloseAsync(false, It.IsAny<CancellationToken>()), Times.Once());
    }
}
