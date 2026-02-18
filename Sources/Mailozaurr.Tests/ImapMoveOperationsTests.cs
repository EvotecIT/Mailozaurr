using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class ImapMoveOperationsTests {
    [Fact]
    public async Task MoveAsync_ReturnsNoOp_WhenSourceAndTargetAreSame() {
        var folder = new FakeFolder("INBOX");

        var result = await ImapMoveOperations.MoveAsync(
            folder,
            folder,
            new[] { new UniqueId(1), new UniqueId(1), new UniqueId(2) });

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Moved);
        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, x => Assert.True(x.Ok));
        Assert.All(result.Results, x => Assert.False(x.UsedCopyFallback));
        Assert.Equal(0, folder.OpenCalls);
        Assert.Equal(0, folder.MoveCalls);
    }

    [Fact]
    public async Task MoveAsync_UsesCopyFallbackAndUidMapping_WhenMoveNotSupported() {
        var source = new FakeFolder("INBOX") {
            NotSupportedMoveUids = new HashSet<uint> { 7 },
            MessageIdHeaders = new Dictionary<uint, string?> {
                [7] = "  <id-7@example>  "
            },
            CopyMap = new Dictionary<uint, uint> {
                [7] = 701
            }
        };
        var target = new FakeFolder("Archive");

        var result = await ImapMoveOperations.MoveAsync(
            source,
            target,
            new[] { new UniqueId(7) });

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Moved);
        var item = Assert.Single(result.Results);
        Assert.True(item.Ok);
        Assert.True(item.UsedCopyFallback);
        Assert.Equal(701, item.TargetUid);
        Assert.Equal("id-7@example", item.MessageId);
        Assert.Equal(1, source.AddFlagsCalls);
        Assert.Equal(1, source.ExpungeCalls);
    }

    [Fact]
    public async Task MoveAsync_UsesSearchLookup_WhenFallbackMapMissing() {
        var source = new FakeFolder("INBOX") {
            NotSupportedMoveUids = new HashSet<uint> { 5 },
            ThrowOnCopyWithMap = true,
            MessageIdHeaders = new Dictionary<uint, string?> {
                [5] = "<lookup-5@example>"
            }
        };
        var target = new FakeFolder("Archive") {
            SearchByMessageId = new Dictionary<string, List<UniqueId>>(StringComparer.OrdinalIgnoreCase) {
                ["lookup-5@example"] = new List<UniqueId> { new UniqueId(5001) }
            }
        };

        var result = await ImapMoveOperations.MoveAsync(
            source,
            target,
            new[] { new UniqueId(5) });

        var item = Assert.Single(result.Results);
        Assert.True(item.Ok);
        Assert.True(item.UsedCopyFallback);
        Assert.Equal(5001, item.TargetUid);
        Assert.Equal(1, source.CopySingleCalls);
        Assert.Equal(1, source.ExpungeCalls);
    }

    [Fact]
    public async Task MoveAsync_ReturnsPerItemFailures_WithSanitizedErrors() {
        var source = new FakeFolder("INBOX") {
            ThrowOnMoveUids = new HashSet<uint> { 2 }
        };
        var target = new FakeFolder("Archive");

        var result = await ImapMoveOperations.MoveAsync(
            source,
            target,
            new[] { new UniqueId(1), new UniqueId(2) },
            sanitizeError: message => "sanitized: " + message);

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Moved);
        Assert.True(result.Results[0].Ok);
        Assert.False(result.Results[1].Ok);
        Assert.Equal("sanitized: move failed for uid 2", result.Results[1].Error);
    }

    [Fact]
    public async Task MoveAsync_PropagatesCancellation_WhenOpenIsCanceled() {
        var source = new FakeFolder("INBOX") {
            ThrowOnOpenWhenCancellationRequested = true
        };
        var target = new FakeFolder("Archive");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ImapMoveOperations.MoveAsync(
                source,
                target,
                new[] { new UniqueId(1) },
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task MoveAsync_RecoversWhenCloseFails_DuringAccessUpgrade() {
        var source = new FakeFolder("INBOX") {
            StartOpen = true,
            StartAccess = FolderAccess.ReadOnly,
            ThrowOnClose = true
        };
        var target = new FakeFolder("Archive");

        var result = await ImapMoveOperations.MoveAsync(
            source,
            target,
            new[] { new UniqueId(1) });

        var item = Assert.Single(result.Results);
        Assert.True(item.Ok);
        Assert.Equal(1, source.CloseCalls);
        Assert.Equal(1, source.OpenCalls);
    }

    private sealed class FakeFolder : ImapMoveOperations.IImapMoveFolder {
        internal FakeFolder(string fullName) {
            FullName = fullName;
        }

        public string FullName { get; }

        private bool _isOpen;
        private FolderAccess _access;

        public bool IsOpen => StartOpen || _isOpen;

        public FolderAccess Access => StartOpen ? StartAccess : _access;

        internal int OpenCalls { get; private set; }
        internal int CloseCalls { get; private set; }
        internal int MoveCalls { get; private set; }
        internal int AddFlagsCalls { get; private set; }
        internal int ExpungeCalls { get; private set; }
        internal int CopySingleCalls { get; private set; }

        internal bool StartOpen { get; set; }
        internal FolderAccess StartAccess { get; set; }
        internal bool ThrowOnOpenWhenCancellationRequested { get; set; }
        internal bool ThrowOnClose { get; set; }
        internal HashSet<uint> NotSupportedMoveUids { get; set; } = new();
        internal HashSet<uint> ThrowOnMoveUids { get; set; } = new();
        internal bool ThrowOnCopyWithMap { get; set; }
        internal Dictionary<uint, string?> MessageIdHeaders { get; set; } = new();
        internal Dictionary<uint, uint> CopyMap { get; set; } = new();
        internal Dictionary<string, List<UniqueId>> SearchByMessageId { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Task OpenAsync(FolderAccess access, CancellationToken cancellationToken = default) {
            if (ThrowOnOpenWhenCancellationRequested) {
                cancellationToken.ThrowIfCancellationRequested();
            }

            OpenCalls++;
            StartOpen = false;
            _isOpen = true;
            _access = access;
            return Task.CompletedTask;
        }

        public Task CloseAsync(bool expunge, CancellationToken cancellationToken = default) {
            _ = expunge;
            _ = cancellationToken;
            CloseCalls++;
            if (ThrowOnClose) {
                throw new InvalidOperationException("close failed");
            }

            StartOpen = false;
            _isOpen = false;
            return Task.CompletedTask;
        }

        public Task<string?> GetMessageIdHeaderAsync(UniqueId uid, CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            MessageIdHeaders.TryGetValue(uid.Id, out var value);
            return Task.FromResult(value);
        }

        public Task MoveToAsync(UniqueId uid, ImapMoveOperations.IImapMoveFolder destination, CancellationToken cancellationToken = default) {
            _ = destination;
            _ = cancellationToken;
            MoveCalls++;

            if (ThrowOnMoveUids.Contains(uid.Id)) {
                throw new InvalidOperationException($"move failed for uid {uid.Id}");
            }
            if (NotSupportedMoveUids.Contains(uid.Id)) {
                throw new NotSupportedException("MOVE is not supported");
            }

            return Task.CompletedTask;
        }

        public Task<IDictionary<UniqueId, UniqueId>?> CopyToWithMapAsync(IReadOnlyCollection<UniqueId> uids, ImapMoveOperations.IImapMoveFolder destination, CancellationToken cancellationToken = default) {
            _ = destination;
            _ = cancellationToken;
            if (ThrowOnCopyWithMap) {
                throw new InvalidOperationException("copy map failed");
            }

            var map = new Dictionary<UniqueId, UniqueId>();
            foreach (var uid in uids) {
                if (CopyMap.TryGetValue(uid.Id, out var targetUid)) {
                    map[uid] = new UniqueId(targetUid);
                }
            }
            return Task.FromResult<IDictionary<UniqueId, UniqueId>?>(map);
        }

        public Task CopyToAsync(UniqueId uid, ImapMoveOperations.IImapMoveFolder destination, CancellationToken cancellationToken = default) {
            _ = uid;
            _ = destination;
            _ = cancellationToken;
            CopySingleCalls++;
            return Task.CompletedTask;
        }

        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default) {
            _ = uid;
            _ = flags;
            _ = silent;
            _ = cancellationToken;
            AddFlagsCalls++;
            return Task.CompletedTask;
        }

        public Task ExpungeAsync(CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            ExpungeCalls++;
            return Task.CompletedTask;
        }

        public Task<IList<UniqueId>> SearchByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            if (SearchByMessageId.TryGetValue(messageId, out var hits)) {
                return Task.FromResult<IList<UniqueId>>(hits);
            }

            return Task.FromResult<IList<UniqueId>>(Array.Empty<UniqueId>());
        }
    }
}
