using Mailozaurr;

namespace Mailozaurr.Tests;

public sealed class DraftMimeMessageFactoryTests {
    [Fact]
    public async Task CreateAsyncBuildsMimeMessageFromDraftAndProfileDefaults() {
        var attachmentPath = CreateTemporaryFilePath("hello.txt");
        File.WriteAllText(attachmentPath, "hello world");

        var factory = new DraftMimeMessageFactory();
        var message = await factory.CreateAsync(
            new MailProfile {
                Id = "gmail-personal",
                DisplayName = "Personal Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultSender = "Sender Name <sender@example.com>"
            },
            new DraftMessage {
                Subject = "Hello",
                TextBody = "plain text",
                HtmlBody = "<b>html</b>",
                Priority = MessagePriority.High,
                To = {
                    new MessageRecipient { Name = "Alice", Address = "alice@example.com" }
                },
                ReplyTo = {
                    new MessageRecipient { Address = "reply@example.com" }
                },
                Headers = {
                    ["X-Test"] = "value"
                },
                Attachments = {
                    new DraftAttachment {
                        Path = attachmentPath
                    }
                }
            });

        Assert.Equal("sender@example.com", message.From.Mailboxes.First().Address);
        Assert.Equal("alice@example.com", message.To.Mailboxes.First().Address);
        Assert.Equal("reply@example.com", message.ReplyTo.Mailboxes.First().Address);
        Assert.Equal("Hello", message.Subject);
        Assert.Equal("plain text", message.TextBody);
        Assert.Equal("<b>html</b>", message.HtmlBody);
        Assert.Equal(MimeKit.MessagePriority.Urgent, message.Priority);
        Assert.Equal("value", message.Headers["X-Test"]);
        Assert.Single(message.Attachments);
    }

    [Fact]
    public async Task CreateAsyncRejectsDraftWithoutRecipients() {
        var factory = new DraftMimeMessageFactory();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(
            new MailProfile {
                Id = "gmail-personal",
                DisplayName = "Personal Gmail",
                Kind = MailProfileKind.Gmail,
                DefaultSender = "sender@example.com"
            },
            new DraftMessage {
                Subject = "Hello"
            }));

        Assert.Contains("recipient", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
