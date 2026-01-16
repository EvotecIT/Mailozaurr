using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpConcurrencyTests
{
    private class CountingClient : ClientSmtp
    {
        private int _active;
        public int Max;

        public override async Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
        {
            var current = Interlocked.Increment(ref _active);
            int initialMax;
            do
            {
                initialMax = Max;
                if (current <= initialMax)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref Max, current, initialMax) != initialMax);

            await Task.Delay(100, cancellationToken);
            Interlocked.Decrement(ref _active);
            return string.Empty;
        }
    }

    [Fact]
    public async Task SendAsync_SerializesConcurrentCalls()
    {
        var smtp = new Smtp();
        var fake = new CountingClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);
        smtp.From = "a@b.com";
        smtp.To = new object[] { "b@c.com" };
        smtp.Subject = "test";
        smtp.TextBody = "body";
        smtp.WebhookUrl = null;
        smtp.CreateMessage();

        var tasks = new[]
        {
            smtp.SendAsync(),
            smtp.SendAsync(),
            smtp.SendAsync()
        };

        await Task.WhenAll(tasks);

        Assert.Equal(1, fake.Max);
    }
}

