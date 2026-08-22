using MailKit.Net.Pop3;
using Mailozaurr;
using MimeKit;

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

            Assert.Equal(Path.Combine(directory, "outside.bin"), destination);
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
