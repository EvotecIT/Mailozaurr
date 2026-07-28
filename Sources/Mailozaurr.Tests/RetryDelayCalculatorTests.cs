namespace Mailozaurr.Tests;

public sealed class RetryDelayCalculatorTests {
    [Fact]
    public void Calculate_BaseDelayAtCapWithJitter_ReturnsCap() {
        var delay = RetryDelayCalculator.Calculate(
            baseDelayMilliseconds: 1000,
            backoff: 2.0,
            attempt: 10,
            maxDelayMilliseconds: 1000,
            jitterMilliseconds: 60_000);

        Assert.Equal(TimeSpan.FromMilliseconds(1000), delay);
    }

    [Fact]
    public void SmtpCalculateRetryDelay_BaseDelayAtCapWithJitter_ReturnsCap() {
        var smtp = new Smtp {
            RetryDelayMilliseconds = 1000,
            RetryDelayBackoff = 2.0,
            MaxDelayMilliseconds = 1000,
            JitterMilliseconds = 60_000
        };

        var delay = smtp.CalculateRetryDelay(10);

        Assert.Equal(TimeSpan.FromMilliseconds(1000), delay);
    }

    [Fact]
    public void GraphCalculateDelay_BaseDelayAtCapWithJitter_ReturnsCap() {
        var policy = new GraphSendPolicy {
            BaseDelayMs = 1000,
            MaxDelayMs = 1000,
            JitterMs = 60_000
        };

        var delay = GraphRetryHelper.CalculateDelay(policy, 10);

        Assert.Equal(TimeSpan.FromMilliseconds(1000), delay);
    }
}
