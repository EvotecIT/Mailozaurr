using System.Threading.Tasks;
using MailKit.Net.Pop3;
using MailKit;
using Xunit;

namespace Mailozaurr.Tests;

public class MessageFlaggerTests {
    private class FakeFolder : MessageFlagSetter.IImapFolder {
        public readonly HashSet<uint> Read = new();
        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            if ((flags & MessageFlags.Seen) != 0)
                Read.Add(uid.Id);
            return Task.CompletedTask;
        }
        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            if ((flags & MessageFlags.Seen) != 0)
                Read.Remove(uid.Id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SetImapFlags_MarksRead() {
        var folder = new FakeFolder();
        await MessageFlagSetter.SetFlagsAsync(folder, new UniqueId(1), MessageFlags.Seen, true);
        Assert.Contains((uint)1, folder.Read);
        await MessageFlagSetter.SetFlagsAsync(folder, new UniqueId(1), MessageFlags.Seen, false);
        Assert.DoesNotContain((uint)1, folder.Read);
    }

    [Fact]
    public async Task SetImapFlags_DryRun_DoesNotChangeState() {
        var folder = new FakeFolder();
        await MessageFlagSetter.SetFlagsAsync(folder, new UniqueId(1), MessageFlags.Seen, true, dryRun: true);
        Assert.DoesNotContain((uint)1, folder.Read);
    }

    [Fact]
    public async Task SetPop3Flags_StoresState() {
        var client = new Pop3Client();
        await MessageFlagSetter.SetReadAsync(client, 2, true);
        Assert.True(MessageFlagSetter.TryGetPop3Read(client, 2, out var read) && read);
        await MessageFlagSetter.SetReadAsync(client, 2, false);
        Assert.True(MessageFlagSetter.TryGetPop3Read(client, 2, out read) && !read);
    }

    [Fact]
    public async Task SetPop3Flags_DryRun_DoesNotStoreState() {
        var client = new Pop3Client();
        await MessageFlagSetter.SetReadAsync(client, 2, true, dryRun: true);
        Assert.False(MessageFlagSetter.TryGetPop3Read(client, 2, out _));
    }
}
