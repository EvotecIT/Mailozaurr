using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class MailDeliveryCooldownTests {
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task ExecuteAsync_ThresholdSuppressesUntilExactExpiry_ThenRestartsFailures() {
        DateTimeOffset now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var cooldown = new MailDeliveryCooldown(() => now);
        int attempts = 0;
        Task Fail(CancellationToken _) { attempts++; throw new ApplicationException("delivery failed"); }

        await Assert.ThrowsAsync<ApplicationException>(() => cooldown.ExecuteAsync(Fail, 2, Window));
        Assert.Equal(1, cooldown.GetSnapshot().FailureCount);
        Assert.Null(cooldown.GetSnapshot().CooldownUntilUtc);
        await Assert.ThrowsAsync<ApplicationException>(() => cooldown.ExecuteAsync(Fail, 2, Window));
        MailDeliveryCooldownSnapshot snapshot = cooldown.GetSnapshot();
        Assert.Equal(2, snapshot.FailureCount);
        Assert.Equal(now.Add(Window), snapshot.CooldownUntilUtc);

        now = now.Add(Window).AddTicks(-1);
        var suppressed = await Assert.ThrowsAsync<InvalidOperationException>(() => cooldown.ExecuteAsync(Fail, 2, Window));
        Assert.Equal("Mail delivery cooldown is active.", suppressed.Message);
        Assert.Equal(2, attempts);
        Assert.Equal(snapshot.CooldownUntilUtc, cooldown.GetSnapshot().CooldownUntilUtc);
        Assert.Equal(2, cooldown.GetSnapshot().FailureCount);

        now = now.AddTicks(1);
        await Assert.ThrowsAsync<ApplicationException>(() => cooldown.ExecuteAsync(Fail, 2, Window));
        Assert.Equal(3, attempts);
        Assert.Equal(1, cooldown.GetSnapshot().FailureCount);
        Assert.Null(cooldown.GetSnapshot().CooldownUntilUtc);
        Assert.Equal(2, snapshot.FailureCount);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task ExecuteAsync_Disabled_BypassesClockAndAllExistingState(int threshold, int minutes) {
        bool clockAllowed = true;
        var cooldown = new MailDeliveryCooldown(() => clockAllowed ? DateTimeOffset.UtcNow : throw new InvalidOperationException("clock must not be read"));
        await Assert.ThrowsAsync<ApplicationException>(() => cooldown.ExecuteAsync(_ => throw new ApplicationException(), 1, Window));
        MailDeliveryCooldownSnapshot before = cooldown.GetSnapshot();
        clockAllowed = false;
        int attempts = 0;
        await cooldown.ExecuteAsync(_ => { attempts++; return Task.CompletedTask; }, threshold, TimeSpan.FromMinutes(minutes));
        await Assert.ThrowsAsync<ApplicationException>(() => cooldown.ExecuteAsync(_ => { attempts++; throw new ApplicationException(); }, threshold, TimeSpan.FromMinutes(minutes)));
        Assert.Equal(2, attempts);
        Assert.Equal(before.FailureCount, cooldown.GetSnapshot().FailureCount);
        Assert.Equal(before.CooldownUntilUtc, cooldown.GetSnapshot().CooldownUntilUtc);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ResetsConsecutiveFailuresAndInstancesAreIndependent() {
        var first = new MailDeliveryCooldown();
        var second = new MailDeliveryCooldown();
        await Assert.ThrowsAsync<ApplicationException>(() => first.ExecuteAsync(_ => throw new ApplicationException(), 2, Window));
        Assert.Equal(0, second.GetSnapshot().FailureCount);
        await first.ExecuteAsync(_ => Task.CompletedTask, 2, Window);
        Assert.Equal(0, first.GetSnapshot().FailureCount);
        Assert.Null(first.GetSnapshot().CooldownUntilUtc);
        await Assert.ThrowsAsync<ApplicationException>(() => first.ExecuteAsync(_ => throw new ApplicationException(), 2, Window));
        Assert.Equal(1, first.GetSnapshot().FailureCount);
        Assert.Null(first.GetSnapshot().CooldownUntilUtc);
    }

    [Fact]
    public async Task ExecuteAsync_CanceledActionAndPreCanceledCall_DoNotCountAsFailures() {
        var cooldown = new MailDeliveryCooldown();
        await Assert.ThrowsAsync<ApplicationException>(() => cooldown.ExecuteAsync(_ => throw new ApplicationException(), 2, Window));
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cooldown.ExecuteAsync(token => {
            Assert.Equal(cancellation.Token, token);
            throw new OperationCanceledException(token);
        }, 2, Window, cancellation.Token));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cooldown.ExecuteAsync(_ => throw new Xunit.Sdk.XunitException("must not execute"), 2, Window, cancellation.Token));
        Assert.Equal(1, cooldown.GetSnapshot().FailureCount);
        Assert.Null(cooldown.GetSnapshot().CooldownUntilUtc);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentAttemptsAndCanceledWait_DoNotOvershootThreshold() {
        var cooldown = new MailDeliveryCooldown();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int attempts = 0;
        Task first = cooldown.ExecuteAsync(async _ => {
            Interlocked.Increment(ref attempts);
            entered.SetResult(true);
            await release.Task;
            throw new ApplicationException();
        }, 1, Window);
        await entered.Task;
        using var cancellation = new CancellationTokenSource();
        Task canceled = cooldown.ExecuteAsync(_ => { Interlocked.Increment(ref attempts); return Task.CompletedTask; }, 1, Window, cancellation.Token);
        Task[] queued = Enumerable.Range(0, 8).Select(_ => cooldown.ExecuteAsync(token => {
            Interlocked.Increment(ref attempts);
            return Task.CompletedTask;
        }, 1, Window)).ToArray();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        release.SetResult(true);
        await Assert.ThrowsAsync<ApplicationException>(() => first);
        foreach (Task attempt in queued) {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => attempt);
            Assert.Equal("Mail delivery cooldown is active.", error.Message);
        }
        Assert.Equal(1, attempts);
        Assert.Equal(1, cooldown.GetSnapshot().FailureCount);
        Assert.NotNull(cooldown.GetSnapshot().CooldownUntilUtc);
    }

    [Fact]
    public async Task ExecuteAsync_Failure_UsesCompletionTimeAndUtcOffset() {
        DateTimeOffset now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2));
        var cooldown = new MailDeliveryCooldown(() => now);
        await Assert.ThrowsAsync<ApplicationException>(() => cooldown.ExecuteAsync(_ => {
            now = now.AddMinutes(3);
            throw new ApplicationException();
        }, 1, Window));
        Assert.Equal(now.ToUniversalTime().Add(Window), cooldown.GetSnapshot().CooldownUntilUtc);
        Assert.Equal(TimeSpan.Zero, cooldown.GetSnapshot().CooldownUntilUtc!.Value.Offset);
    }
}
