using System.Text;
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
    public async Task BatchExportUsesDistinctDeterministicPathsAndOfficeImoEvidence() {
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
                ["same:a"] = first,
                ["same?a"] = second
            });
            var service = new MailEmlExportService(profileStore, new[] { source });

            var result = await service.ExportAsync(new MailEmlExportRequest {
                ProfileId = "mailbox",
                FolderId = "INBOX",
                MessageIds = new List<string> { "same:a", "same?a", "same:a" },
                DestinationDirectory = directory
            });

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.RequestedCount);
            Assert.Equal(2, result.ExportedCount);
            Assert.Equal(0, result.FailedCount);
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
                ["message"] = CreateMessage("message", "X-Test: value")
            });
            var service = new MailEmlExportService(profileStore, new[] { source });
            var request = new MailEmlExportRequest {
                ProfileId = "mailbox",
                FolderId = "INBOX",
                MessageIds = new List<string> { "message" },
                DestinationDirectory = directory
            };
            var first = await service.ExportAsync(request);
            var path = Assert.Single(first.Results).DestinationPath!;
            var original = File.ReadAllBytes(path);

            var second = await service.ExportAsync(request);

            Assert.False(second.Succeeded);
            Assert.Equal("destination_exists", Assert.Single(second.Results).Code);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(1, source.RequestCount);
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
                    ["message"] = CreateMessage("message", "X-Test: value")
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

    private sealed class FakeRawMailMessageSource : IRawMailMessageSource {
        private readonly IReadOnlyDictionary<string, byte[]> _messages;

        internal FakeRawMailMessageSource(
            IReadOnlyDictionary<string, byte[]> messages,
            MailProfileKind kind = MailProfileKind.Imap) {
            _messages = messages;
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public int RequestCount { get; private set; }

        public Task<RawMailMessage?> GetRawMessageAsync(
            MailProfile profile,
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            RequestCount++;
            return Task.FromResult(_messages.TryGetValue(request.MessageId, out var content)
                ? new RawMailMessage { MessageId = request.MessageId, Content = content }
                : null);
        }
    }
}
