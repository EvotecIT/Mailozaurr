using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Xunit;
using Mailozaurr;

namespace Mailozaurr.Tests;

public class ImapIdleListenerTests {
    [Fact]
    public void Constructor_SetsSearchQuery() {
        var client = new ImapClient();
        var query = SearchQuery.SubjectContains("Test");

        var listener = new ImapIdleListener(client, searchQuery: query);
        var field = typeof(ImapIdleListener).GetField("_searchQuery", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(listener);

        Assert.Equal(query, field);
    }

    [Fact]
    public async Task StopAsync_CancelsIdleLoopGracefully() {
        var listener = new ImapIdleListener(new ImapClient());
        var cancellation = new CancellationTokenSource();
        var idleCompletion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        SetPrivateField(listener, "_cancel", cancellation);
        SetPrivateField(listener, "_idleTask", idleCompletion.Task);

        var stopTask = listener.StopAsync();

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(stopTask.IsCompleted, "StopAsync completed before the idle loop finished.");

        idleCompletion.SetResult(null);

        await stopTask;

        Assert.Null(GetPrivateField<CancellationTokenSource?>(listener, "_cancel"));
        Assert.Null(GetPrivateField<Task?>(listener, "_idleTask"));
    }

    [Fact]
    public async Task StopAsync_PropagatesExceptionsFromIdleLoop() {
        var listener = new ImapIdleListener(new ImapClient());
        var cancellation = new CancellationTokenSource();
        var idleFailure = new InvalidOperationException("Idle loop faulted");

        SetPrivateField(listener, "_cancel", cancellation);
        SetPrivateField(listener, "_idleTask", Task.FromException(idleFailure));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => listener.StopAsync());

        Assert.Same(idleFailure, exception);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Null(GetPrivateField<Task?>(listener, "_idleTask"));
        Assert.Null(GetPrivateField<CancellationTokenSource?>(listener, "_cancel"));
    }

    [Fact]
    public async Task StopAsync_IgnoresCancellationFromIdleLoopWhenStopping() {
        var listener = new ImapIdleListener(new ImapClient());
        var cancellation = new CancellationTokenSource();

        SetPrivateField(listener, "_cancel", cancellation);
        SetPrivateField(listener, "_idleTask", Task.FromCanceled(new CancellationToken(canceled: true)));

        await listener.StopAsync();

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Null(GetPrivateField<Task?>(listener, "_idleTask"));
        Assert.Null(GetPrivateField<CancellationTokenSource?>(listener, "_cancel"));
    }

    [Fact]
    public void Dispose_IgnoresCancellationFromIdleLoopWhenStopping() {
        var listener = new ImapIdleListener(new ImapClient());
        var cancellation = new CancellationTokenSource();

        SetPrivateField(listener, "_cancel", cancellation);
        SetPrivateField(listener, "_idleTask", Task.FromCanceled(new CancellationToken(canceled: true)));

        listener.Dispose();

        Assert.Null(GetPrivateField<Task?>(listener, "_idleTask"));
        Assert.Null(GetPrivateField<CancellationTokenSource?>(listener, "_cancel"));
    }

    private static void SetPrivateField<T>(ImapIdleListener listener, string name, T value) =>
        typeof(ImapIdleListener).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(listener, value);

    private static T? GetPrivateField<T>(ImapIdleListener listener, string name) =>
        (T?)typeof(ImapIdleListener).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(listener);
}
