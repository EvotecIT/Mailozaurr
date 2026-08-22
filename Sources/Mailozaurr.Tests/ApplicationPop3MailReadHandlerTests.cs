using MailKit.Net.Pop3;
using MailKit;
using Mailozaurr;
using MimeKit;
using System.Text;

namespace Mailozaurr.Tests;

public sealed class ApplicationPop3MailReadHandlerTests {
    [Fact]
    public async Task HandlerUsesInjectedSearchDelegate() {
        var handler = new Pop3MailReadHandler(
            new FakePop3SessionFactory(),
            searchAsync: (client, profile, request, cancellationToken) =>
                Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                    new MessageSummary {
                        ProfileId = profile.Id,
                        Id = "uid:message-42",
                        FolderId = "INBOX",
                        Subject = request.QueryText
                    }
                }));

        var results = await handler.SearchAsync(CreateProfile(), new MailSearchRequest {
            ProfileId = "work-pop3",
            QueryText = "reports"
        });

        Assert.Single(results);
        Assert.Equal("uid:message-42", results[0].Id);
        Assert.Equal("INBOX", results[0].FolderId);
        Assert.Equal("reports", results[0].Subject);
    }

    [Fact]
    public void MessageIdsPreferUidAndUseContentHashWhenUidlIsUnavailable() {
        var message = new MimeMessage { Subject = "Stable content" };
        message.Body = new TextPart("plain") { Text = "Body" };

        Assert.Equal("uid:stable-id", Pop3MailReadHandler.FormatMessageId("stable-id", message));
        var fallback = Pop3MailReadHandler.FormatMessageId(null, message);
        Assert.StartsWith("hash:", fallback, StringComparison.Ordinal);
        Assert.EndsWith(":0", fallback, StringComparison.Ordinal);
        Assert.Equal(("stable-id", null, 0), Pop3MailReadHandler.ParseMessageId("uid:stable-id"));
        var parsedFallback = Pop3MailReadHandler.ParseMessageId(fallback);
        Assert.Null(parsedFallback.Uid);
        Assert.Equal(0, parsedFallback.Occurrence);
        Assert.Equal(fallback.Substring(5, fallback.Length - 7), parsedFallback.Fingerprint);
        Assert.Equal(parsedFallback.Fingerprint, Pop3MailReadHandler.ParseMessageId("hash:" + parsedFallback.Fingerprint).Fingerprint);
        Assert.Throws<InvalidOperationException>(() => Pop3MailReadHandler.ParseMessageId("index:4"));
        Assert.Throws<InvalidOperationException>(() => Pop3MailReadHandler.ParseMessageId("4"));
        Assert.Throws<InvalidOperationException>(() => Pop3MailReadHandler.ParseMessageId("hash:value:not-a-number"));

        message.Subject = "Changed content";
        Assert.NotEqual(fallback, Pop3MailReadHandler.FormatMessageId(null, message));
    }

    [Fact]
    public void HashFallbackIdsDisambiguateByteIdenticalMailboxEntries() {
        var message = new MimeMessage { Subject = "Duplicate" };
        message.Body = new TextPart("plain") { Text = "Same bytes" };

        var first = Pop3MailReadHandler.FormatMessageId(null, message, occurrence: 0);
        var second = Pop3MailReadHandler.FormatMessageId(null, message, occurrence: 1);

        Assert.NotEqual(first, second);
        var firstId = Pop3MailReadHandler.ParseMessageId(first);
        var secondId = Pop3MailReadHandler.ParseMessageId(second);
        Assert.Equal(firstId.Fingerprint, secondId.Fingerprint);
        Assert.Equal(0, firstId.Occurrence);
        Assert.Equal(1, secondId.Occurrence);
    }

    [Theory]
    [InlineData("uid:stable-id")]
    [InlineData("stable-id")]
    public void Pop3UidAliasesUseOneCanonicalStorageIdentity(string requestedId) {
        var message = new MimeMessage { Subject = "Stable content" };
        message.Body = new TextPart("plain") { Text = "Body" };
        var snapshot = new Pop3MailboxBrowser.Pop3ResolvedMessageSnapshot(0, "stable-id", 10, message);

        var canonical = Pop3MailReadHandler.CanonicalizeMessageIdForStorage(
            Pop3MailReadHandler.ParseMessageId(requestedId),
            snapshot);

        Assert.Equal("uid:stable-id", canonical);
    }

    [Fact]
    public async Task HashFallbackIdsResolveTheIntendedDuplicateOccurrence() {
        var duplicate = new MimeMessage { Subject = "Duplicate" };
        duplicate.Body = new TextPart("plain") { Text = "Same bytes" };
        var downloads = new List<int>();
        var handler = new Pop3MailReadHandler(
            new DuplicateMessagePop3SessionFactory(new[] { duplicate, duplicate }, downloads));

        var results = await handler.SearchAsync(CreateProfile(), new MailSearchRequest {
            ProfileId = "work-pop3"
        });

        Assert.Equal(2, results.Count);
        Assert.NotEqual(results[0].Id, results[1].Id);

        downloads.Clear();
        var first = await handler.GetMessageAsync(CreateProfile(), new GetMessageRequest {
            ProfileId = "work-pop3",
            MessageId = results[0].Id
        });
        Assert.NotNull(first);
        Assert.Equal(new[] { 1 }, downloads);

        downloads.Clear();
        var second = await handler.GetMessageAsync(CreateProfile(), new GetMessageRequest {
            ProfileId = "work-pop3",
            MessageId = results[1].Id
        });
        Assert.NotNull(second);
        Assert.Equal(new[] { 1, 0 }, downloads);
    }

    [Fact]
    public async Task HashOccurrencesCountMatchingMessagesThatHaveUids() {
        var duplicate = new MimeMessage { Subject = "Mixed UIDL" };
        duplicate.Body = new TextPart("plain") { Text = "Same bytes" };
        var downloads = new List<int>();
        var handler = new Pop3MailReadHandler(
            new MixedUidlPop3SessionFactory(new[] { duplicate, duplicate }, downloads));

        var results = await handler.SearchAsync(CreateProfile(), new MailSearchRequest {
            ProfileId = "work-pop3"
        });

        Assert.Equal(2, results.Count);
        Assert.StartsWith("hash:", results[0].Id, StringComparison.Ordinal);
        Assert.Equal("uid:older-uid", results[1].Id);

        downloads.Clear();
        var detail = await handler.GetMessageAsync(CreateProfile(), new GetMessageRequest {
            ProfileId = "work-pop3",
            MessageId = results[0].Id
        });

        Assert.NotNull(detail);
        Assert.Equal(new[] { 1 }, downloads);
    }

    [Fact]
    public async Task HashFallbackIdRemainsResolvableWhenUidlRecovers() {
        var message = new MimeMessage { Subject = "Transient UIDL" };
        message.Body = new TextPart("plain") { Text = "Same message" };
        var handler = new Pop3MailReadHandler(new RecoveringUidlPop3SessionFactory(message));

        var result = Assert.Single(await handler.SearchAsync(CreateProfile(), new MailSearchRequest {
            ProfileId = "work-pop3"
        }));
        Assert.StartsWith("hash:", result.Id, StringComparison.Ordinal);

        var detail = await handler.GetMessageAsync(CreateProfile(), new GetMessageRequest {
            ProfileId = "work-pop3",
            MessageId = result.Id
        });

        Assert.NotNull(detail);
        Assert.Equal(result.Id, detail!.Id);
        Assert.Equal(result.Id, detail.Summary!.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INBOX")]
    [InlineData(" inbox ")]
    public void Pop3FolderAliasesNormalizeToOneStorageIdentity(string? folderId) {
        var normalized = Pop3MailReadHandler.NormalizeFolderId(folderId);
        var identity = MimeAttachmentStorage.CreateStorageIdentity(
            "work-pop3", "Pop3", normalized, "message", "attachment");

        Assert.Equal("INBOX", normalized);
        Assert.Equal(
            MimeAttachmentStorage.CreateStorageIdentity(
                "work-pop3", "Pop3", "INBOX", "message", "attachment"),
            identity);
    }

    [Fact]
    public void SummaryMappingUsesNormalizedPop3IdentityAndInbox() {
        var message = new MimeMessage {
            Subject = "Quarterly report",
            Date = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero),
            Body = new TextPart("plain") { Text = "Ready" }
        };
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));

        var summary = Pop3MailReadHandler.MapSummary("work-pop3", "uid-7", message);

        Assert.Equal("uid:uid-7", summary.Id);
        Assert.Equal("INBOX", summary.FolderId);
        Assert.Equal("Quarterly report", summary.Subject);
        Assert.Equal("sender@example.com", Assert.Single(summary.From).Address);
        Assert.Equal("recipient@example.com", Assert.Single(summary.To).Address);
    }

    [Fact]
    public void AttachmentDestinationUsesOnlyTheRemoteFileNameBase() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var attachment = new MimePart("application", "octet-stream") {
                FileName = @"..\outside.bin"
            };

            var destination = MimeAttachmentStorage.ResolveDestinationPath(directory, attachment);

            Assert.Equal(directory, Path.GetDirectoryName(destination));
            Assert.StartsWith("outside~", Path.GetFileName(destination), StringComparison.Ordinal);
            Assert.EndsWith(".bin", destination, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SanitizedAttachmentNamesPreserveDistinctRemoteIdentity() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var colon = MimeAttachmentStorage.ResolveDestinationPath(directory, "report:a.txt");
            var question = MimeAttachmentStorage.ResolveDestinationPath(directory, "report?a.txt");

            Assert.NotEqual(colon, question);
            Assert.StartsWith("report_a~", Path.GetFileName(colon), StringComparison.Ordinal);
            Assert.StartsWith("report_a~", Path.GetFileName(question), StringComparison.Ordinal);
            Assert.EndsWith(".txt", colon, StringComparison.Ordinal);
            Assert.EndsWith(".txt", question, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RepeatedAttachmentNamesUsePerPartIdentity() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var first = MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", "0");
            var second = MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", "1");

            Assert.NotEqual(first, second);
            Assert.Equal(first, MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", "0"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void NumericAndFileNameAttachmentAliasesResolveToOneCanonicalIdentity() {
        var attachment = new MimePart("application", "octet-stream") { FileName = "report.txt" };
        IReadOnlyList<MimeEntity> attachments = new[] { attachment };

        var numericIndex = MimeAttachmentStorage.ResolveAttachmentIndex(attachments, "0");
        var fileNameIndex = MimeAttachmentStorage.ResolveAttachmentIndex(attachments, "report.txt");

        Assert.Equal(0, numericIndex);
        Assert.Equal(numericIndex, fileNameIndex);
        Assert.Equal(
            MimeAttachmentStorage.CreateStorageIdentity("profile", "message", numericIndex.ToString()),
            MimeAttachmentStorage.CreateStorageIdentity("profile", "message", fileNameIndex.ToString()));
    }

    [Fact]
    public void UnnamedAttachmentUsesDeterministicPerPartFallback() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var attachment = new MimePart("application", "octet-stream");
            var firstIdentity = MimeAttachmentStorage.CreateStorageIdentity("profile-a", "message-a", "0");
            var secondIdentity = MimeAttachmentStorage.CreateStorageIdentity("profile-a", "message-a", "1");

            var first = MimeAttachmentStorage.ResolveDestinationPath(directory, attachment, firstIdentity);
            var repeated = MimeAttachmentStorage.ResolveDestinationPath(directory, attachment, firstIdentity);
            var second = MimeAttachmentStorage.ResolveDestinationPath(directory, attachment, secondIdentity);

            Assert.Equal(first, repeated);
            Assert.NotEqual(first, second);
            Assert.StartsWith("attachment-", Path.GetFileName(first), StringComparison.Ordinal);
            Assert.EndsWith(".bin", first, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UnnamedAttachmentFallbackAliasResolvesToTheListedPart() {
        IReadOnlyList<MimeEntity> attachments = new MimeEntity[] {
            new MimePart("application", "octet-stream"),
            new MimePart("application", "octet-stream")
        };
        string Identity(int index) => MimeAttachmentStorage.CreateStorageIdentity(
            "profile-a",
            "message-a",
            index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var listedName = MimeAttachmentStorage.GetAttachmentFileName(attachments[1], Identity(1));

        var resolved = MimeAttachmentStorage.ResolveAttachmentIndex(attachments, listedName, Identity);

        Assert.Equal(1, resolved);
    }

    [Fact]
    public void BatchAttachmentNamesIncludeProfileFolderAndMessageIdentity() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var firstIdentity = MimeAttachmentStorage.CreateStorageIdentity("profile-a", "Pop3", "INBOX", "message-a", "0");
            var otherMessageIdentity = MimeAttachmentStorage.CreateStorageIdentity("profile-a", "Pop3", "INBOX", "message-b", "0");
            var otherFolderIdentity = MimeAttachmentStorage.CreateStorageIdentity("profile-a", "Imap", "Archive", "message-a", "0");
            var otherProfileIdentity = MimeAttachmentStorage.CreateStorageIdentity("profile-b", "Pop3", "INBOX", "message-a", "0");

            var first = MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", firstIdentity);
            var paths = new[] {
                first,
                MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", otherMessageIdentity),
                MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", otherFolderIdentity),
                MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", otherProfileIdentity)
            };

            Assert.Equal(4, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(first, MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt", firstIdentity));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AttachmentNameIdentityIsCaseAndSeparatorSensitive() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var lower = MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt");
            var upper = MimeAttachmentStorage.ResolveDestinationPath(directory, "REPORT.txt");
            var slash = MimeAttachmentStorage.ResolveDestinationPath(directory, "folder/report.txt");
            var backslash = MimeAttachmentStorage.ResolveDestinationPath(directory, @"folder\report.txt");

            Assert.Equal(4, new[] { lower, upper, slash, backslash }.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(lower, MimeAttachmentStorage.ResolveDestinationPath(directory, "report.txt"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GeneratedAttachmentNameCannotCollideWithAnotherRemoteName() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var transformed = MimeAttachmentStorage.ResolveDestinationPath(directory, "report:a.txt");
            var generatedLookingRemoteName = Path.GetFileName(transformed);
            var generatedLooking = MimeAttachmentStorage.ResolveDestinationPath(directory, generatedLookingRemoteName);

            Assert.NotEqual(transformed, generatedLooking);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("LPT1.log")]
    [InlineData("NUL.tar.gz")]
    [InlineData("CON.backup.txt")]
    [InlineData("trailing. ")]
    public void PortableUnsafeAttachmentNamesAreDisambiguated(string remoteFileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var destination = MimeAttachmentStorage.ResolveDestinationPath(directory, remoteFileName);

            Assert.Equal(directory, Path.GetDirectoryName(destination));
            Assert.Contains("~", Path.GetFileName(destination), StringComparison.Ordinal);
            Assert.False(Path.GetFileName(destination).StartsWith("CON.", StringComparison.OrdinalIgnoreCase));
            Assert.False(Path.GetFileName(destination).StartsWith("NUL.", StringComparison.OrdinalIgnoreCase));
            Assert.False(Path.GetFileName(destination).StartsWith("LPT1.", StringComparison.OrdinalIgnoreCase));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AttachmentNameHashingKeepsPortableComponentLength() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var remoteFileName = new string('ż', 190) + ":x." + new string('ę', 80);
            var destination = MimeAttachmentStorage.ResolveDestinationPath(directory, remoteFileName);
            var fileName = Path.GetFileName(destination);

            Assert.True(Encoding.UTF8.GetByteCount(fileName) <= 193);
            Assert.Contains("~", fileName, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MailProfile CreateProfile() => new() {
        Id = "work-pop3",
        DisplayName = "Work POP3",
        Kind = MailProfileKind.Pop3,
        Settings = new Dictionary<string, string> {
            [MailProfileSettingsKeys.Server] = "pop.example.com"
        }
    };

    private sealed class FakePop3SessionFactory : IPop3SessionFactory {
        public Task<Pop3Client> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Pop3Client());
    }

    private sealed class DuplicateMessagePop3SessionFactory : IPop3SessionFactory {
        private readonly IReadOnlyList<MimeMessage> _messages;
        private readonly List<int> _downloads;

        public DuplicateMessagePop3SessionFactory(IReadOnlyList<MimeMessage> messages, List<int> downloads) {
            _messages = messages;
            _downloads = downloads;
        }

        public Task<Pop3Client> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult<Pop3Client>(new DuplicateMessagePop3Client(_messages, _downloads));
    }

    private sealed class RecoveringUidlPop3SessionFactory : IPop3SessionFactory {
        private readonly MimeMessage _message;
        private int _connectionCount;

        public RecoveringUidlPop3SessionFactory(MimeMessage message) {
            _message = message;
        }

        public Task<Pop3Client> ConnectAsync(
            MailProfile profile,
            CancellationToken cancellationToken = default) {
            var uidlAvailable = Interlocked.Increment(ref _connectionCount) > 1;
            return Task.FromResult<Pop3Client>(new RecoveringUidlPop3Client(_message, uidlAvailable));
        }
    }

    private sealed class MixedUidlPop3SessionFactory : IPop3SessionFactory {
        private readonly IReadOnlyList<MimeMessage> _messages;
        private readonly List<int> _downloads;

        public MixedUidlPop3SessionFactory(IReadOnlyList<MimeMessage> messages, List<int> downloads) {
            _messages = messages;
            _downloads = downloads;
        }

        public Task<Pop3Client> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult<Pop3Client>(new MixedUidlPop3Client(_messages, _downloads));
    }

    private sealed class MixedUidlPop3Client : DuplicateMessagePop3Client {
        public MixedUidlPop3Client(IReadOnlyList<MimeMessage> messages, List<int> downloads)
            : base(messages, downloads) { }

        public override Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken = default) =>
            index == 0
                ? Task.FromResult("older-uid")
                : Task.FromException<string>(new Pop3CommandException("UIDL failed transiently."));
    }

    private sealed class RecoveringUidlPop3Client : Pop3Client {
        private readonly MimeMessage _message;
        private readonly bool _uidlAvailable;

        public RecoveringUidlPop3Client(MimeMessage message, bool uidlAvailable) {
            _message = message;
            _uidlAvailable = uidlAvailable;
        }

        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => 1;

        public override Task<MimeMessage> GetMessageAsync(
            int index,
            CancellationToken cancellationToken = default,
            ITransferProgress? progress = null) => Task.FromResult(_message);

        public override Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken = default) =>
            _uidlAvailable
                ? Task.FromResult("recovered-uid")
                : Task.FromException<string>(new Pop3CommandException("UIDL failed transiently."));
    }

    private class DuplicateMessagePop3Client : Pop3Client {
        private readonly IReadOnlyList<MimeMessage> _messages;
        private readonly List<int> _downloads;

        public DuplicateMessagePop3Client(IReadOnlyList<MimeMessage> messages, List<int> downloads) {
            _messages = messages;
            _downloads = downloads;
        }

        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => _messages.Count;

        public override Task<MimeMessage> GetMessageAsync(
            int index,
            CancellationToken cancellationToken = default,
            ITransferProgress? progress = null) {
            _downloads.Add(index);
            return Task.FromResult(_messages[index]);
        }

        public override Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken = default) =>
            Task.FromException<string>(new NotSupportedException("UIDL is unavailable."));
    }
}
