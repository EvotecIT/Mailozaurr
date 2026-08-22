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
        Assert.Equal(new[] { 0 }, downloads);

        downloads.Clear();
        var second = await handler.GetMessageAsync(CreateProfile(), new GetMessageRequest {
            ProfileId = "work-pop3",
            MessageId = results[1].Id
        });
        Assert.NotNull(second);
        Assert.Equal(new[] { 0, 1 }, downloads);
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

    private sealed class DuplicateMessagePop3Client : Pop3Client {
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
