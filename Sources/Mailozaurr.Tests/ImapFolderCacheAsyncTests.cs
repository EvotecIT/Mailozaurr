using MailKit;
using MailKit.Net.Imap;
using Moq;

namespace Mailozaurr.Tests;

public sealed class ImapFolderCacheAsyncTests {
    [Fact]
    public async Task Resolution_UsesAsyncIoAndReusesTheOpenedFolder() {
        var inbox = Mock.Of<IMailFolder>(folder => folder.FullName == "INBOX");
        var folder = new Mock<IMailFolder>(MockBehavior.Strict);
        folder.SetupGet(value => value.IsOpen).Returns(false);
        folder.SetupGet(value => value.Access).Returns(FolderAccess.ReadOnly);
        folder.Setup(value => value.OpenAsync(FolderAccess.ReadOnly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FolderAccess.ReadOnly)
            .Callback(() => folder.SetupGet(value => value.IsOpen).Returns(true));
        var client = new Mock<ImapClient> { CallBase = true };
        using var clientLifetime = client.Object;
        client.SetupGet(value => value.Inbox).Returns(inbox);
        client.Setup(value => value.GetFolderAsync("Archive", It.IsAny<CancellationToken>())).ReturnsAsync(folder.Object);
        Assert.Same(folder.Object, await client.Object.GetCachedFolderAsync("Archive", FolderAccess.ReadOnly));
        Assert.Same(folder.Object, await client.Object.GetCachedFolderAsync("Archive", FolderAccess.ReadOnly));
        client.Verify(value => value.GetFolderAsync("Archive", It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(value => value.GetFolder("Archive", It.IsAny<CancellationToken>()), Times.Never);
        folder.Verify(value => value.OpenAsync(FolderAccess.ReadOnly, It.IsAny<CancellationToken>()), Times.Once);
        client.Object.ClearFolderCache();
    }

    [Fact]
    public async Task CanceledResolution_DoesNotOpenOrResolveAFolder() {
        var client = new Mock<ImapClient> { CallBase = true };
        using var clientLifetime = client.Object;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Object.GetCachedFolderAsync("Archive", FolderAccess.ReadWrite, cancellation.Token));
        client.VerifyNoOtherCalls();
    }
}
