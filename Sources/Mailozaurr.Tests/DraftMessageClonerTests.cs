using Mailozaurr.Hosting;

namespace Mailozaurr.Tests;

public sealed class DraftMessageClonerTests {
    [Fact]
    public void Clone_PreservesPriorityAndDetachesMutableValues() {
        var source = new DraftMessage {
            ProfileId = "profile",
            Priority = MessagePriority.High,
            To = {
                new MessageRecipient {
                    Name = "Recipient",
                    Address = "recipient@example.com"
                }
            },
            Headers = {
                ["X-Test"] = "value"
            },
            Attachments = {
                new DraftAttachment {
                    Path = "report.pdf",
                    FileName = "renamed.pdf"
                }
            }
        };

        DraftMessage clone = DraftMessageCloner.Clone(source);
        clone.To[0].Address = "changed@example.com";
        clone.Headers["X-Test"] = "changed";
        clone.Attachments[0].FileName = "changed.pdf";

        Assert.Equal(MessagePriority.High, clone.Priority);
        Assert.Equal("recipient@example.com", source.To[0].Address);
        Assert.Equal("value", source.Headers["X-Test"]);
        Assert.Equal("renamed.pdf", source.Attachments[0].FileName);
    }
}
