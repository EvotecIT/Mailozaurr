using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class ImapBulkFlagOperationsTests {
    [Fact]
    public async Task SetFlagsAsync_UsesBulkPath_WhenBulkSucceeds() {
        var folder = new FakeFolder();

        var result = await ImapBulkFlagOperations.SetFlagsAsync(
            folder,
            new[] { new UniqueId(1), new UniqueId(2), new UniqueId(2) },
            MessageFlags.Seen,
            add: true);

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Updated);
        Assert.Equal(2, result.SuccessfulUids.Count);
        Assert.All(result.Results, x => Assert.True(x.Ok));
        Assert.Equal(1, folder.BulkAddCalls);
        Assert.Equal(0, folder.SingleAddCalls);
    }

    [Fact]
    public async Task SetFlagsAsync_FallsBackPerItem_WhenBulkFails() {
        var folder = new FakeFolder {
            ThrowOnBulkRemove = true,
            FailSingleRemoveFor = new HashSet<uint> { 2 }
        };

        var result = await ImapBulkFlagOperations.SetFlagsAsync(
            folder,
            new[] { new UniqueId(1), new UniqueId(2) },
            MessageFlags.Flagged,
            add: false,
            sanitizeError: message => "sanitized: " + message);

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Updated);
        Assert.Single(result.SuccessfulUids);
        Assert.Equal((uint)1, result.SuccessfulUids[0].Id);
        Assert.Equal(2, result.Results.Count);
        Assert.True(result.Results[0].Ok);
        Assert.False(result.Results[1].Ok);
        Assert.Equal("sanitized: uid 2 failed", result.Results[1].Error);
        Assert.Equal(1, folder.BulkRemoveCalls);
        Assert.Equal(2, folder.SingleRemoveCalls);
    }

    private sealed class FakeFolder : ImapBulkFlagOperations.IImapFolder {
        internal bool ThrowOnBulkRemove { get; set; }
        internal HashSet<uint> FailSingleRemoveFor { get; set; } = new();
        internal int BulkAddCalls { get; private set; }
        internal int BulkRemoveCalls { get; private set; }
        internal int SingleAddCalls { get; private set; }
        internal int SingleRemoveCalls { get; private set; }

        public Task AddFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = uids ?? throw new ArgumentNullException(nameof(uids));
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            BulkAddCalls++;
            return Task.CompletedTask;
        }

        public Task RemoveFlagsAsync(IReadOnlyCollection<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = uids ?? throw new ArgumentNullException(nameof(uids));
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            BulkRemoveCalls++;
            if (ThrowOnBulkRemove) {
                throw new InvalidOperationException("bulk failed");
            }
            return Task.CompletedTask;
        }

        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = uid;
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            SingleAddCalls++;
            return Task.CompletedTask;
        }

        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            SingleRemoveCalls++;
            if (FailSingleRemoveFor.Contains(uid.Id)) {
                throw new InvalidOperationException($"uid {uid.Id} failed");
            }
            return Task.CompletedTask;
        }
    }
}
