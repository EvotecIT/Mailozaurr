using MailKit;
using MailKit.Net.Pop3;
using MimeKit;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class MailboxSearcherBodyTests {
    [Fact]
    public async Task SearchPop3Async_BodyContains_FiltersMessages() {
        var msg1 = new MimeMessage();
        msg1.Body = new TextPart("plain") { Text = "hello world" };
        var msg2 = new MimeMessage();
        msg2.Body = new TextPart("plain") { Text = "other" };
        var client = new FakePop3Client(new[] { msg1, msg2 });
        var result = await MailboxSearcher.SearchPop3Async(client, bodyContains: "hello");
        Assert.Single(result);
    }

    [Fact]
    public async Task SearchPop3Async_UnqualifiedQueryFiltersHeaderAndBodyBeforeLimit() {
        var unrelated = new MimeMessage { Subject = "Unrelated" };
        unrelated.Body = new TextPart("plain") { Text = "Other content" };
        var matching = new MimeMessage { Subject = "Invoice 42" };
        matching.Body = new TextPart("plain") { Text = "Approved for payment" };
        var alsoMatching = new MimeMessage { Subject = "Invoice 43" };
        alsoMatching.Body = new TextPart("plain") { Text = "Approved for payment" };
        var client = new FakePop3Client(new[] { unrelated, matching, alsoMatching });

        var result = await MailboxSearcher.SearchPop3Async(
            client,
            maxResults: 1,
            queryString: "Invoice approved");

        var message = Assert.Single(result);
        Assert.Equal(1, message.Index);
        Assert.Equal("Invoice 42", message.Message.Subject);
    }

    [Fact]
    public async Task SearchPop3Async_HeaderOnlyFilterDownloadsBodiesOnlyForMatches() {
        var unrelated = new MimeMessage { Subject = "Unrelated" };
        unrelated.Body = new TextPart("plain") { Text = new string('x', 100_000) };
        var matching = new MimeMessage { Subject = "Invoice 42" };
        matching.Body = new TextPart("plain") { Text = new string('y', 100_000) };
        var client = new FakePop3Client(new[] { unrelated, matching });

        var result = await MailboxSearcher.SearchPop3Async(client, subject: "Invoice");

        Assert.Single(result);
        Assert.Equal(2, client.HeaderDownloads);
        Assert.Equal(1, client.FullMessageDownloads);
    }

    [Fact]
    public async Task SearchPop3Async_AddressHeaderFilterUsesParsedMailboxSemantics() {
        var misleading = new MimeMessage();
        misleading.Headers.Add(HeaderId.From, "Sender <sender@example.com> (comment-only-token)");
        misleading.Body = new TextPart("plain") { Text = "Not a match" };
        var matching = new MimeMessage();
        matching.Headers.Add(HeaderId.From, "Team: Alice <alice@example.com>, Bob <bob@example.com>;");
        matching.Body = new TextPart("plain") { Text = "Match" };
        var client = new FakePop3Client(new[] { misleading, matching });

        var falsePositiveResult = await MailboxSearcher.SearchPop3Async(client, fromContains: "comment-only-token");

        Assert.Empty(falsePositiveResult);
        Assert.Equal(2, client.HeaderDownloads);
        Assert.Equal(0, client.FullMessageDownloads);

        var groupResult = await MailboxSearcher.SearchPop3Async(client, fromContains: "bob@example.com");

        Assert.Single(groupResult);
        Assert.Equal(4, client.HeaderDownloads);
        Assert.Equal(1, client.FullMessageDownloads);
    }

    private sealed class FakePop3Client : Pop3Client {
        private readonly List<MimeMessage> _messages;
        public FakePop3Client(IEnumerable<MimeMessage> messages) => _messages = new List<MimeMessage>(messages);
        public int HeaderDownloads { get; private set; }
        public int FullMessageDownloads { get; private set; }
        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => _messages.Count;
        public override Task<HeaderList> GetMessageHeadersAsync(int index, CancellationToken cancellationToken = default) {
            HeaderDownloads++;
            return Task.FromResult(_messages[index].Headers);
        }
        public override Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken = default, ITransferProgress? progress = null) {
            FullMessageDownloads++;
            return Task.FromResult(_messages[index]);
        }
    }
}
