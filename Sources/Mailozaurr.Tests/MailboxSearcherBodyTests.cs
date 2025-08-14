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

    private sealed class FakePop3Client : Pop3Client {
        private readonly List<MimeMessage> _messages;
        public FakePop3Client(IEnumerable<MimeMessage> messages) => _messages = new List<MimeMessage>(messages);
        public override bool IsConnected => true;
        public override bool IsAuthenticated => true;
        public override int Count => _messages.Count;
        public override Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
            => Task.FromResult(_messages[index]);
    }
}
