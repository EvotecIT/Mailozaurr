using MailKit.Net.Imap;
using Mailozaurr.Hosting;

namespace Mailozaurr.Tests;

public sealed class ApplicationImapMailReadHandlerTests {
    [Fact]
    public async Task HandlerUsesInjectedSearchDelegate() {
        var handler = new ImapMailReadHandler(
            new FakeImapSessionFactory(),
            searchAsync: (client, profile, request, cancellationToken) => Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                new MessageSummary {
                    ProfileId = profile.Id,
                    Id = "42",
                    Subject = request.QueryText
                }
            }));

        var results = await handler.SearchAsync(
            new MailProfile {
                Id = "work-imap",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                Settings = new Dictionary<string, string> {
                    [MailProfileSettingsKeys.Server] = "imap.example.com"
                }
            },
            new MailSearchRequest {
                ProfileId = "work-imap",
                QueryText = "reports"
            });

        Assert.Single(results);
        Assert.Equal("42", results[0].Id);
        Assert.Equal("reports", results[0].Subject);
    }

    [Fact]
    public async Task HandlerUsesInjectedAttachmentDelegate() {
        var handler = new ImapMailReadHandler(
            new FakeImapSessionFactory(),
            saveAttachmentAsync: (client, profile, request, cancellationToken) =>
                Task.FromResult(OperationResult.Success($"Saved {request.AttachmentId}")));

        var result = await handler.SaveAttachmentAsync(
            new MailProfile {
                Id = "work-imap",
                DisplayName = "Work IMAP",
                Kind = MailProfileKind.Imap,
                Settings = new Dictionary<string, string> {
                    [MailProfileSettingsKeys.Server] = "imap.example.com"
                }
            },
            new SaveAttachmentRequest {
                ProfileId = "work-imap",
                MessageId = "10",
                AttachmentId = "0",
                DestinationPath = Path.Combine(Path.GetTempPath(), "attachment.bin")
            });

        Assert.True(result.Succeeded);
        Assert.Contains("Saved 0", result.Message, StringComparison.Ordinal);
    }

    private sealed class FakeImapSessionFactory : IImapSessionFactory {
        public Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImapClient());
    }
}