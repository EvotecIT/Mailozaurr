using MailKit.Net.Pop3;
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
        Assert.Equal(("stable-id", null), Pop3MailReadHandler.ParseMessageId("uid:stable-id"));
        Assert.Equal((null, fallback.Substring(5)), Pop3MailReadHandler.ParseMessageId(fallback));
        Assert.Throws<InvalidOperationException>(() => Pop3MailReadHandler.ParseMessageId("index:4"));
        Assert.Throws<InvalidOperationException>(() => Pop3MailReadHandler.ParseMessageId("4"));

        message.Subject = "Changed content";
        Assert.NotEqual(fallback, Pop3MailReadHandler.FormatMessageId(null, message));
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
}
