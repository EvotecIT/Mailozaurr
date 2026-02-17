using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class ImapDeleteOperationsTests {
    [Fact]
    public async Task DeleteAsync_Expunges_WhenRequestedAndAtLeastOneDeleteSucceeds() {
        var folder = new FakeDeleteFolder {
            ThrowOnBulkAdd = true,
            FailSingleAddFor = new HashSet<uint> { 2 }
        };

        var result = await ImapDeleteOperations.DeleteAsync(
            folder,
            new[] { new UniqueId(1), new UniqueId(2) },
            expunge: true,
            sanitizeError: message => "sanitized: " + message);

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Deleted);
        Assert.True(result.Expunged);
        Assert.Equal(1, folder.ExpungeCalls);
        Assert.Single(result.DeletedUids);
        Assert.Equal((uint)1, result.DeletedUids[0].Id);
        Assert.Equal(2, result.Results.Count);
        Assert.True(result.Results[0].Ok);
        Assert.False(result.Results[1].Ok);
        Assert.Equal("sanitized: uid 2 failed", result.Results[1].Error);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotExpunge_WhenNoDeleteSucceeds() {
        var folder = new FakeDeleteFolder {
            ThrowOnBulkAdd = true,
            FailSingleAddFor = new HashSet<uint> { 1 }
        };

        var result = await ImapDeleteOperations.DeleteAsync(
            folder,
            new[] { new UniqueId(1) },
            expunge: true);

        Assert.Equal(1, result.Requested);
        Assert.Equal(0, result.Deleted);
        Assert.False(result.Expunged);
        Assert.Equal(0, folder.ExpungeCalls);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotExpunge_WhenNotRequested() {
        var folder = new FakeDeleteFolder();

        var result = await ImapDeleteOperations.DeleteAsync(
            folder,
            new[] { new UniqueId(1) },
            expunge: false);

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Deleted);
        Assert.False(result.Expunged);
        Assert.Equal(0, folder.ExpungeCalls);
    }

    private sealed class FakeDeleteFolder : ImapDeleteOperations.IImapDeleteFolder {
        internal bool ThrowOnBulkAdd { get; set; }
        internal HashSet<uint> FailSingleAddFor { get; set; } = new();
        internal int ExpungeCalls { get; private set; }

        public string FullName => "INBOX";

        public Task AddFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = uids ?? throw new ArgumentNullException(nameof(uids));
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            if (ThrowOnBulkAdd) {
                throw new InvalidOperationException("bulk failed");
            }
            return Task.CompletedTask;
        }

        public Task RemoveFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = uids;
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            if (FailSingleAddFor.Contains(uid.Id)) {
                throw new InvalidOperationException($"uid {uid.Id} failed");
            }
            return Task.CompletedTask;
        }

        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = uid;
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task ExpungeAsync(CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            ExpungeCalls++;
            return Task.CompletedTask;
        }
    }
}
