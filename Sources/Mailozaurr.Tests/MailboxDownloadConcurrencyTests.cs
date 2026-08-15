using MimeKit;

namespace Mailozaurr.Tests;

public sealed class MailboxDownloadConcurrencyTests {
    [Fact]
    public Task GraphDownloadsAreBoundedAndRemainInMailboxOrder() => VerifyAsync(
        (ids, limit, download, token) => GraphMailboxSearcher.DownloadMimeMessagesAsync(
            ids, limit, download, token));

    [Fact]
    public Task GmailDownloadsAreBoundedAndRemainInMailboxOrder() => VerifyAsync(
        (ids, limit, download, token) => GmailMailboxSearcher.DownloadMimeMessagesAsync(
            ids, limit, download, token));

    private static async Task VerifyAsync(
        Func<IReadOnlyList<string>, int,
            Func<string, CancellationToken, Task<MimeMessage>>, CancellationToken,
            Task<IReadOnlyList<MimeMessage>>> downloadAll) {
        string[] ids = Enumerable.Range(0, 12).Select(index => $"message-{index:00}").ToArray();
        int active = 0;
        int peak = 0;

        IReadOnlyList<MimeMessage> messages = await downloadAll(ids, 3, async (id, token) => {
            int current = Interlocked.Increment(ref active);
            int observed;
            while (current > (observed = Volatile.Read(ref peak))) {
                if (Interlocked.CompareExchange(ref peak, current, observed) == observed) break;
            }
            try {
                await Task.Delay(20, token);
                return new MimeMessage { Subject = id };
            } finally {
                Interlocked.Decrement(ref active);
            }
        }, CancellationToken.None);

        Assert.InRange(peak, 2, 3);
        Assert.Equal(ids, messages.Select(message => message.Subject));
        foreach (MimeMessage message in messages) message.Dispose();
    }
}
