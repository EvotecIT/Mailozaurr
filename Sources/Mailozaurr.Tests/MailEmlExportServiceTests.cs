using System.Text;
using MailKit;
using MailKit.Net.Pop3;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class MailEmlExportServiceTests {
    [Fact]
    public async Task ProviderWriterPreservesValidatedSourceBytesExactly() {
        var directory = CreateTemporaryDirectory();
        try {
            var content = CreateMessage("raw-one", "X-Spacing:   keep\t");
            var path = Path.Combine(directory, "message.eml");

            var result = await new ProviderEmlArtifactWriter().WriteAsync(
                content,
                path,
                maxBytes: 1024 * 1024);

            Assert.True(result.UsedPreservedSource);
            Assert.Equal(content.LongLength, result.BytesWritten);
            Assert.Equal(content, File.ReadAllBytes(path));
            Assert.Equal(64, result.Sha256.Length);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderWriterRejectsLimitBeforeReplacingDestination() {
        var directory = CreateTemporaryDirectory();
        try {
            var path = Path.Combine(directory, "message.eml");
            var original = Encoding.ASCII.GetBytes("existing");
            File.WriteAllBytes(path, original);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ProviderEmlArtifactWriter().WriteAsync(
                    CreateMessage("too-large", "X-Test: value"),
                    path,
                    maxBytes: 8));

            Assert.Equal(original, File.ReadAllBytes(path));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GmailRawDecoderRejectsOversizedEncodedInputBeforeDecoding() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            RawMailMessageSourceUtilities.DecodeBase64Url("QUJDREVGRw", maxBytes: 3));

        Assert.Contains("3 byte export limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pop3HashExportSkipsOversizedNewerMessageWithMatchingHeaders() {
        var requested = new MimeMessage { Subject = "Requested" };
        requested.Headers.Add("X-Identity", "shared");
        requested.Body = new TextPart("plain") { Text = "small" };
        var unrelated = new MimeMessage { Subject = "Requested" };
        unrelated.Headers.Add("X-Identity", "shared");
        unrelated.Body = new TextPart("plain") { Text = new string('x', 4096) };
        var client = new BoundedPop3Client(new[] { requested, unrelated }, oversizedIndex: 1);
        var source = new Pop3RawMailMessageSource(new FixedPop3SessionFactory(client));
        using var session = await source.OpenSessionAsync(
            new MailProfile { Id = "pop", Kind = MailProfileKind.Pop3 });

        var result = await session.GetRawMessageAsync(new RawMailMessageRequest {
            MessageId = Pop3MailReadHandler.FormatMessageId(null, requested),
            MaxBytes = 2048
        });

        Assert.NotNull(result);
        Assert.Equal(new[] { 0 }, client.Downloads);
        using var stream = new MemoryStream(result!.Content, writable: false);
        Assert.Equal("Requested", MimeMessage.Load(stream).Subject);
    }

    [Fact]
    public async Task Pop3HashExportReportsInconclusiveOversizedCandidate() {
        var oversized = new MimeMessage { Subject = "Oversized" };
        oversized.Body = new TextPart("plain") { Text = new string('x', 4096) };
        var client = new BoundedPop3Client(new[] { oversized }, oversizedIndex: 0);
        var source = new Pop3RawMailMessageSource(new FixedPop3SessionFactory(client));
        using var session = await source.OpenSessionAsync(
            new MailProfile { Id = "pop", Kind = MailProfileKind.Pop3 });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            session.GetRawMessageAsync(new RawMailMessageRequest {
                MessageId = Pop3MailReadHandler.FormatMessageId(null, oversized),
                MaxBytes = 2048
            }));

        Assert.Contains("cannot be resolved", exception.Message, StringComparison.Ordinal);
        Assert.Empty(client.Downloads);
    }

    [Fact]
    public async Task Pop3HashResolutionScansMailboxOnlyOncePerBatchSession() {
        var first = new MimeMessage { Subject = "First", Body = new TextPart("plain") { Text = "one" } };
        var second = new MimeMessage { Subject = "Second", Body = new TextPart("plain") { Text = "two" } };
        var client = new BoundedPop3Client(new[] { first, second }, oversizedIndex: -1);
        var source = new Pop3RawMailMessageSource(new FixedPop3SessionFactory(client));
        using var session = await source.OpenSessionAsync(
            new MailProfile { Id = "pop", Kind = MailProfileKind.Pop3 });

        var missingFirst = await session.GetRawMessageAsync(new RawMailMessageRequest {
            MessageId = "hash:missing-first:0",
            MaxBytes = 2048
        });
        var missingSecond = await session.GetRawMessageAsync(new RawMailMessageRequest {
            MessageId = "hash:missing-second:0",
            MaxBytes = 2048
        });

        Assert.Null(missingFirst);
        Assert.Null(missingSecond);
        Assert.Equal(new[] { 1, 0 }, client.Downloads);
    }

    [Fact]
    public async Task ProviderWriterClaimsOrReplacesDestinationAtomically() {
        var directory = CreateTemporaryDirectory();
        try {
            var path = Path.Combine(directory, "message.eml");
            var original = CreateMessage("original", "X-Test: original");
            var replacement = CreateMessage("replacement", "X-Test: replacement");
            File.WriteAllBytes(path, original);
            var writer = new ProviderEmlArtifactWriter();

            await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(
                replacement,
                path,
                maxBytes: 1024 * 1024));
            Assert.Equal(original, File.ReadAllBytes(path));

            var result = await writer.WriteAsync(
                replacement,
                path,
                maxBytes: 1024 * 1024,
                overwrite: true);

            Assert.True(result.UsedPreservedSource);
            Assert.Equal(replacement, File.ReadAllBytes(path));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentProviderWritersCannotBothClaimOneDestination() {
        var directory = CreateTemporaryDirectory();
        try {
            var path = Path.Combine(directory, "message.eml");
            var first = CreateMessage("first", "X-Test: first");
            var second = CreateMessage("second", "X-Test: second");
            var writer = new ProviderEmlArtifactWriter();

            async Task<bool> TryWriteAsync(byte[] content) {
                try {
                    await writer.WriteAsync(content, path, maxBytes: 1024 * 1024);
                    return true;
                } catch (IOException) {
                    return false;
                }
            }

            var outcomes = await Task.WhenAll(TryWriteAsync(first), TryWriteAsync(second));

            Assert.Single(outcomes, succeeded => succeeded);
            Assert.Single(outcomes, succeeded => !succeeded);
            var saved = File.ReadAllBytes(path);
            Assert.True(saved.SequenceEqual(first) || saved.SequenceEqual(second));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BatchExportUsesOneSessionWithDistinctDeterministicPathsAndOfficeImoEvidence() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "mailbox",
                DisplayName = "Mailbox",
                Kind = MailProfileKind.Imap
            });
            var first = CreateMessage("first", "X-Test: one");
            var second = CreateMessage("second", "X-Test: two");
            var source = new FakeRawMailMessageSource(new Dictionary<string, byte[]> {
                ["41"] = first,
                ["42"] = second
            });
            var service = new MailEmlExportService(profileStore, new[] { source });

            var result = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "mailbox",
                FolderId = "INBOX",
                MessageIds = new List<string> { "41", "42", "41" },
                DestinationDirectory = directory
            });

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.RequestedCount);
            Assert.Equal(2, result.ExportedCount);
            Assert.Equal(0, result.FailedCount);
            Assert.Equal(1, source.SessionCount);
            Assert.Equal(1, source.DisposeCount);
            Assert.Equal(2, result.Results.Select(item => item.DestinationPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(result.Results, item => {
                Assert.True(item.Succeeded);
                Assert.True(item.UsedPreservedSource);
                Assert.NotNull(item.DestinationPath);
                Assert.EndsWith(".eml", item.DestinationPath!, StringComparison.OrdinalIgnoreCase);
            });
            Assert.Contains(result.Results, item => File.ReadAllBytes(item.DestinationPath!).SequenceEqual(first));
            Assert.Contains(result.Results, item => File.ReadAllBytes(item.DestinationPath!).SequenceEqual(second));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BatchExportReportsExistingDestinationWithoutOverwriting() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "mailbox",
                DisplayName = "Mailbox",
                Kind = MailProfileKind.Imap
            });
            var source = new FakeRawMailMessageSource(new Dictionary<string, byte[]> {
                ["42"] = CreateMessage("message", "X-Test: value")
            });
            var service = new MailEmlExportService(profileStore, new[] { source });
            var request = new MailEmlExportRequest {
                ProfileId = "mailbox",
                FolderId = "INBOX",
                MessageIds = new List<string> { "42" },
                DestinationDirectory = directory
            };
            var first = await service.ExportAsync(request);
            var path = Assert.Single(first.Results).DestinationPath!;
            var original = File.ReadAllBytes(path);

            var second = await service.ExportAsync(request);

            Assert.False(second.Succeeded);
            Assert.Equal("destination_exists", Assert.Single(second.Results).Code);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(2, source.RequestCount);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BatchExportRejectsOversizedBatchBeforeProviderWork() {
        var profileStore = new InMemoryMailProfileStore();
        await profileStore.SaveAsync(new MailProfile {
            Id = "mailbox",
            DisplayName = "Mailbox",
            Kind = MailProfileKind.Imap
        });
        var source = new FakeRawMailMessageSource(new Dictionary<string, byte[]>());
        var service = new MailEmlExportService(profileStore, new[] { source });

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExportAsync(
            new MailEmlExportRequest {
                ProfileId = "mailbox",
                MessageIds = Enumerable.Range(1, MailEmlExportService.MaximumBatchMessageCount + 1)
                    .Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .ToList(),
                DestinationDirectory = Path.GetTempPath()
            }));

        Assert.Equal("MessageIds", exception.ParamName);
        Assert.Equal(0, source.RequestCount);
    }

    [Fact]
    public async Task ImapFolderAndUidAliasesUseOneDeterministicDestination() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "imap-mailbox",
                DisplayName = "IMAP mailbox",
                Kind = MailProfileKind.Imap
            });
            var content = CreateMessage("message", "X-Test: value");
            var source = new FakeRawMailMessageSource(new Dictionary<string, byte[]> {
                ["042"] = content,
                ["42"] = content
            });
            var service = new MailEmlExportService(profileStore, new[] { source });

            var first = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "imap-mailbox",
                FolderId = " inbox ",
                MessageIds = new List<string> { "042", "42" },
                DestinationDirectory = directory
            });
            var second = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "imap-mailbox",
                FolderId = "INBOX",
                MessageIds = new List<string> { "42" },
                DestinationDirectory = directory,
                Overwrite = true
            });

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(1, first.RequestedCount);
            Assert.Equal(2, source.RequestCount);
            Assert.Equal(
                Assert.Single(first.Results).DestinationPath,
                Assert.Single(second.Results).DestinationPath);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Pop3UidAliasesAreCanonicalizedBeforeDeduplication() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "pop-mailbox",
                DisplayName = "POP mailbox",
                Kind = MailProfileKind.Pop3
            });
            var source = new FakeRawMailMessageSource(
                new Dictionary<string, byte[]> {
                    ["uid:abc"] = CreateMessage("message", "X-Test: value")
                },
                MailProfileKind.Pop3);
            var service = new MailEmlExportService(profileStore, new[] { source });

            var result = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "pop-mailbox",
                MessageIds = new List<string> { "abc", "UID:abc" },
                DestinationDirectory = directory
            });

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.RequestedCount);
            Assert.Equal("uid:abc", Assert.Single(result.Results).MessageId);
            Assert.Equal(1, source.RequestCount);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("abc", "uid:abc")]
    [InlineData("UID:abc", "uid:abc")]
    [InlineData("hash:AbC", "hash:AbC:0")]
    [InlineData("HASH:AbC:00", "hash:AbC:0")]
    public void Pop3SupportedIdentifiersHaveOneCanonicalForm(string input, string expected) =>
        Assert.Equal(expected, Pop3MailReadHandler.CanonicalizeMessageIdForStorage(input));

    [Fact]
    public async Task GraphRawSourceReturnsNullForProviderNotFound() {
        var source = new GraphRawMailMessageSource(
            new StatusGraphSessionFactory(System.Net.HttpStatusCode.NotFound));
        using var session = await source.OpenSessionAsync(
            new MailProfile { Id = "graph", Kind = MailProfileKind.Graph });

        var result = await session.GetRawMessageAsync(
            new RawMailMessageRequest { MessageId = "missing", MaxBytes = 1024 });

        Assert.Null(result);
    }

    [Fact]
    public async Task GmailRawSourceReturnsNullForProviderNotFound() {
        var source = new GmailRawMailMessageSource(
            new StatusGmailSessionFactory(System.Net.HttpStatusCode.NotFound));
        using var session = await source.OpenSessionAsync(
            new MailProfile { Id = "gmail", Kind = MailProfileKind.Gmail });

        var result = await session.GetRawMessageAsync(
            new RawMailMessageRequest { MessageId = "missing", MaxBytes = 1024 });

        Assert.Null(result);
    }

    [Fact]
    public async Task GraphMeAliasesUseOneDeterministicDestination() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "graph-mailbox",
                DisplayName = "Graph mailbox",
                Kind = MailProfileKind.Graph
            });
            var source = new FakeRawMailMessageSource(
                new Dictionary<string, byte[]> {
                    ["message"] = CreateMessage("message", "X-Test: value")
                },
                MailProfileKind.Graph);
            var service = new MailEmlExportService(profileStore, new[] { source });

            var first = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "graph-mailbox",
                MailboxId = "ME",
                MessageIds = { "message" },
                DestinationDirectory = directory
            });
            var second = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "graph-mailbox",
                MailboxId = "me",
                MessageIds = { "message" },
                DestinationDirectory = directory,
                Overwrite = true
            });

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(
                Assert.Single(first.Results).DestinationPath,
                Assert.Single(second.Results).DestinationPath);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GraphConfiguredMailboxAliasesUseOneDeterministicDestination() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "graph-mailbox",
                DisplayName = "Graph mailbox",
                Kind = MailProfileKind.Graph,
                DefaultMailbox = "owner@example.com"
            });
            var source = new FakeRawMailMessageSource(
                new Dictionary<string, byte[]> {
                    ["message"] = CreateMessage("message", "X-Test: value")
                },
                MailProfileKind.Graph);
            var service = new MailEmlExportService(profileStore, new[] { source });

            var first = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "graph-mailbox",
                MailboxId = "me",
                MessageIds = { "message" },
                DestinationDirectory = directory
            });
            var second = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "graph-mailbox",
                MailboxId = "OWNER@example.com",
                MessageIds = { "message" },
                DestinationDirectory = directory,
                Overwrite = true
            });

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(
                Assert.Single(first.Results).DestinationPath,
                Assert.Single(second.Results).DestinationPath);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GmailAuthenticatedMailboxAliasesUseOneDeterministicDestination() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "gmail-mailbox",
                DisplayName = "Gmail mailbox",
                Kind = MailProfileKind.Gmail,
                DefaultMailbox = "owner@example.com"
            });
            var source = new FakeRawMailMessageSource(
                new Dictionary<string, byte[]> {
                    ["message"] = CreateMessage("message", "X-Test: value")
                },
                MailProfileKind.Gmail);
            var service = new MailEmlExportService(profileStore, new[] { source });

            var first = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "gmail-mailbox",
                MailboxId = "me",
                MessageIds = { "message" },
                DestinationDirectory = directory
            });
            var second = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "gmail-mailbox",
                MailboxId = "OWNER@example.com",
                MessageIds = { "message" },
                DestinationDirectory = directory,
                Overwrite = true
            });

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(
                Assert.Single(first.Results).DestinationPath,
                Assert.Single(second.Results).DestinationPath);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderTimeoutIsReportedPerItemAndBatchContinues() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "mailbox",
                DisplayName = "Mailbox",
                Kind = MailProfileKind.Imap
            });
            var source = new FakeRawMailMessageSource(new Dictionary<string, byte[]> {
                ["42"] = CreateMessage("message", "X-Test: value")
            }) {
                ThrowOperationCanceledForMessageId = "41"
            };
            var service = new MailEmlExportService(profileStore, new[] { source });

            var result = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "mailbox",
                MessageIds = new List<string> { "41", "42" },
                DestinationDirectory = directory
            });

            Assert.False(result.Succeeded);
            Assert.Equal(1, result.ExportedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal("eml_export_failed", result.Results[0].Code);
            Assert.True(result.Results[1].Succeeded);
            Assert.Equal(2, source.RequestCount);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CallerCancellationStillAbortsBatch() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "mailbox",
                DisplayName = "Mailbox",
                Kind = MailProfileKind.Imap
            });
            using var cancellation = new CancellationTokenSource();
            var source = new FakeRawMailMessageSource(new Dictionary<string, byte[]>()) {
                ThrowOperationCanceledForMessageId = "41",
                CancelOnOperationCanceled = cancellation
            };
            var service = new MailEmlExportService(profileStore, new[] { source });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExportAsync(
                new MailEmlExportRequest {
                    ProfileId = "mailbox",
                    MessageIds = new List<string> { "41", "42" },
                    DestinationDirectory = directory
                },
                cancellation.Token));

            Assert.Equal(1, source.RequestCount);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImapUidValidityCreatesDistinctStorageEpochs() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "imap-mailbox",
                DisplayName = "IMAP mailbox",
                Kind = MailProfileKind.Imap
            });
            var source = new FakeRawMailMessageSource(new Dictionary<string, byte[]> {
                ["42"] = CreateMessage("message", "X-Test: value")
            }) {
                StorageIdentityComponent = "uidvalidity:100"
            };
            var service = new MailEmlExportService(profileStore, new[] { source });
            var request = new MailEmlExportRequest {
                ProfileId = "imap-mailbox",
                FolderId = "INBOX",
                MessageIds = new List<string> { "42" },
                DestinationDirectory = directory
            };

            var first = await service.ExportAsync(request);
            source.StorageIdentityComponent = "uidvalidity:200";
            var second = await service.ExportAsync(request);

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.NotEqual(
                Assert.Single(first.Results).DestinationPath,
                Assert.Single(second.Results).DestinationPath);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Pop3FolderAliasesUseOneDeterministicDestination() {
        var directory = CreateTemporaryDirectory();
        try {
            var profileStore = new InMemoryMailProfileStore();
            await profileStore.SaveAsync(new MailProfile {
                Id = "pop-mailbox",
                DisplayName = "POP mailbox",
                Kind = MailProfileKind.Pop3
            });
            var source = new FakeRawMailMessageSource(
                new Dictionary<string, byte[]> {
                    ["uid:message"] = CreateMessage("message", "X-Test: value")
                },
                MailProfileKind.Pop3);
            var service = new MailEmlExportService(profileStore, new[] { source });

            var first = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "pop-mailbox",
                MessageIds = new List<string> { "message" },
                DestinationDirectory = directory
            });
            var second = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "pop-mailbox",
                FolderId = " inbox ",
                MessageIds = new List<string> { "message" },
                DestinationDirectory = directory,
                Overwrite = true
            });

            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(
                Assert.Single(first.Results).DestinationPath,
                Assert.Single(second.Results).DestinationPath);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CreateMessage(string subject, string extraHeader) => Encoding.ASCII.GetBytes(
        "From: sender@example.com\r\n" +
        "To: recipient@example.com\r\n" +
        "Subject: " + subject + "\r\n" +
        "Date: Sat, 22 Aug 2026 12:00:00 +0000\r\n" +
        "Message-Id: <" + subject + "@example.com>\r\n" +
        extraHeader + "\r\n" +
        "Content-Type: text/plain; charset=us-ascii\r\n" +
        "\r\n" +
        "Body for " + subject + "\r\n");

    private static string CreateTemporaryDirectory() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeRawMailMessageSource : IRawMailMessageSource, IRawMailMessageSession {
        private readonly IReadOnlyDictionary<string, byte[]> _messages;

        internal FakeRawMailMessageSource(
            IReadOnlyDictionary<string, byte[]> messages,
            MailProfileKind kind = MailProfileKind.Imap) {
            _messages = messages;
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public int RequestCount { get; private set; }

        public int SessionCount { get; private set; }

        public int DisposeCount { get; private set; }

        public string? StorageIdentityComponent { get; set; }

        public string? ThrowOperationCanceledForMessageId { get; set; }

        public CancellationTokenSource? CancelOnOperationCanceled { get; set; }

        public Task<IRawMailMessageSession> OpenSessionAsync(
            MailProfile profile,
            CancellationToken cancellationToken = default) {
            SessionCount++;
            return Task.FromResult<IRawMailMessageSession>(this);
        }

        public Task<RawMailMessage?> GetRawMessageAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            RequestCount++;
            if (string.Equals(request.MessageId, ThrowOperationCanceledForMessageId, StringComparison.Ordinal)) {
                CancelOnOperationCanceled?.Cancel();
                throw new OperationCanceledException("Provider operation timed out.", cancellationToken);
            }
            return Task.FromResult(_messages.TryGetValue(request.MessageId, out var content)
                ? new RawMailMessage {
                    MessageId = request.MessageId,
                    Content = content,
                    StorageIdentityComponent = StorageIdentityComponent
                }
                : null);
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class FixedPop3SessionFactory : IPop3SessionFactory {
        private readonly Pop3Client _client;

        internal FixedPop3SessionFactory(Pop3Client client) => _client = client;

        public Task<Pop3Client> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(_client);
    }

    private sealed class StatusGraphSessionFactory : IGraphSessionFactory {
        private readonly System.Net.HttpStatusCode _statusCode;

        internal StatusGraphSessionFactory(System.Net.HttpStatusCode statusCode) => _statusCode = statusCode;

        public Task<GraphSession> ConnectAsync(
            MailProfile profile,
            CancellationToken cancellationToken = default) {
            var client = new GraphApiClient(new OAuthCredential {
                UserName = "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            });
            SetHttpClient(client, new StatusResponseHandler(_statusCode), "https://graph.microsoft.com/v1.0/");
            return Task.FromResult(new GraphSession(client, "me"));
        }
    }

    private sealed class StatusGmailSessionFactory : IGmailSessionFactory {
        private readonly System.Net.HttpStatusCode _statusCode;

        internal StatusGmailSessionFactory(System.Net.HttpStatusCode statusCode) => _statusCode = statusCode;

        public Task<GmailSession> ConnectAsync(
            MailProfile profile,
            CancellationToken cancellationToken = default) {
            var client = new GmailApiClient(new OAuthCredential {
                UserName = "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            });
            SetHttpClient(client, new StatusResponseHandler(_statusCode), "https://gmail.googleapis.com/gmail/v1/");
            return Task.FromResult(new GmailSession(client, "me"));
        }
    }

    private sealed class StatusResponseHandler : System.Net.Http.HttpMessageHandler {
        private readonly System.Net.HttpStatusCode _statusCode;

        internal StatusResponseHandler(System.Net.HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new System.Net.Http.HttpResponseMessage(_statusCode) {
                Content = new System.Net.Http.StringContent("{}")
            });
    }

    private static void SetHttpClient(object client, System.Net.Http.HttpMessageHandler handler, string baseAddress) {
        var field = client.GetType().GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(client, new System.Net.Http.HttpClient(handler) { BaseAddress = new Uri(baseAddress) });
    }

    private sealed class BoundedPop3Client : Pop3Client {
        private readonly IReadOnlyList<MimeMessage> _messages;
        private readonly int _oversizedIndex;

        internal BoundedPop3Client(IReadOnlyList<MimeMessage> messages, int oversizedIndex) {
            _messages = messages;
            _oversizedIndex = oversizedIndex;
        }

        public List<int> Downloads { get; } = new();

        public override bool IsConnected => true;

        public override bool IsAuthenticated => true;

        public override int Count => _messages.Count;

        public override int GetMessageSize(int index, CancellationToken cancellationToken = default) =>
            index == _oversizedIndex ? 4096 : GetBytes(_messages[index]).Length;

        public override Task<HeaderList> GetMessageHeadersAsync(
            int index,
            CancellationToken cancellationToken = default) => Task.FromResult(_messages[index].Headers);

        public override Task<Stream> GetStreamAsync(
            int index,
            bool headersOnly = false,
            CancellationToken cancellationToken = default,
            ITransferProgress? progress = null) {
            Downloads.Add(index);
            return Task.FromResult<Stream>(new MemoryStream(GetBytes(_messages[index]), writable: false));
        }

        private static byte[] GetBytes(MimeMessage message) {
            using var stream = new MemoryStream();
            message.WriteTo(stream);
            return stream.ToArray();
        }
    }
}
