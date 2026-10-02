namespace Mailozaurr.Tests;

public sealed class GraphRequestLimiterTests {
    [Fact]
    public async Task ReducingLimit_DrainsExistingRequestsBeforeAdmittingWaiters() {
        var limiter = new GraphRequestLimiter(2);
        await limiter.WaitAsync(default);
        await limiter.WaitAsync(default);
        var waiting = limiter.WaitAsync(default);
        limiter.Limit = 1;
        limiter.Release();
        Assert.False(waiting.IsCompleted);
        limiter.Release();
        await waiting;
        var next = limiter.WaitAsync(default);
        Assert.False(next.IsCompleted);
        limiter.Limit = 2;
        await next;
        limiter.Release();
        limiter.Release();
    }

    [Fact]
    public async Task CanceledWaiter_DoesNotConsumeCapacityOrBlockItsSuccessor() {
        var limiter = new GraphRequestLimiter(1);
        await limiter.WaitAsync(default);
        using var cancellation = new CancellationTokenSource();
        var canceled = limiter.WaitAsync(cancellation.Token);
        var next = limiter.WaitAsync(default);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        limiter.Release();
        await next;
        limiter.Release();
        await limiter.WaitAsync(default);
        limiter.Release();
    }
}
