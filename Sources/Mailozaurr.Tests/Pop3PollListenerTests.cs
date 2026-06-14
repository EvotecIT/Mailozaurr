using MailKit.Net.Pop3;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class Pop3PollListenerTests {
    [Fact]
    public void Dispose_DisposesCancellationTokenSourceAndNullsField() {
        var listener = new Pop3PollListener(new Pop3Client());
        var field = typeof(Pop3PollListener).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pollingTaskField = typeof(Pop3PollListener).GetField("_pollingTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var cts = new CancellationTokenSource();
        field.SetValue(listener, cts);
        pollingTaskField.SetValue(listener, Task.CompletedTask);

        listener.Dispose();

        Assert.Null(field.GetValue(listener));
        Assert.Null(pollingTaskField.GetValue(listener));
        Assert.Throws<ObjectDisposedException>(() => _ = cts.Token.WaitHandle);
    }

    [Fact]
    public async Task DisposeAsync_DisposesCancellationTokenSourceAndNullsField() {
        var listener = new Pop3PollListener(new Pop3Client());
        var field = typeof(Pop3PollListener).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pollingTaskField = typeof(Pop3PollListener).GetField("_pollingTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var cts = new CancellationTokenSource();
        field.SetValue(listener, cts);
        pollingTaskField.SetValue(listener, Task.CompletedTask);

        await listener.DisposeAsync();

        Assert.Null(field.GetValue(listener));
        Assert.Null(pollingTaskField.GetValue(listener));
        Assert.Throws<ObjectDisposedException>(() => _ = cts.Token.WaitHandle);
    }

    [Fact]
    public async Task StartAsync_FailureDuringInitialSnapshot_CleansUpState() {
        var listener = new Pop3PollListener(new Pop3Client());
        var cancelField = typeof(Pop3PollListener).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pollingTaskField = typeof(Pop3PollListener).GetField("_pollingTask", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await Assert.ThrowsAnyAsync<Exception>(() => listener.StartAsync());

        Assert.Null(cancelField.GetValue(listener));
        Assert.Null(pollingTaskField.GetValue(listener));

        await Assert.ThrowsAnyAsync<Exception>(() => listener.StartAsync());
    }

    [Fact]
    public async Task StartAsync_RaisesEventsForNewUidsOnly() {
        var listener = new TestPop3PollListener();
        listener.SetMessages(
            new TestPop3PollListener.TestMessage("uid1", CreateMessage("Initial")));
        var receivedSubjects = new List<string>();
        listener.MessageArrived += (_, message) => receivedSubjects.Add(message.Message.Subject ?? string.Empty);

        await listener.StartAsync();

        await listener.WaitForDelayAsync();
        listener.SetMessages(
            new TestPop3PollListener.TestMessage("uid1", CreateMessage("Initial")),
            new TestPop3PollListener.TestMessage("uid2", CreateMessage("New")));
        listener.ReleaseNextDelay();

        await listener.WaitForDelayAsync();

        await listener.StopAsync();

        Assert.Single(receivedSubjects);
        Assert.Equal("New", receivedSubjects[0]);
    }

    [Fact]
    public async Task PollLoop_HandlesDeletionsAndReorderedIndexes() {
        var listener = new TestPop3PollListener();
        listener.SetMessages(
            new TestPop3PollListener.TestMessage("uid1", CreateMessage("First")),
            new TestPop3PollListener.TestMessage("uid2", CreateMessage("Second")));
        var receivedSubjects = new List<string>();
        listener.MessageArrived += (_, message) => receivedSubjects.Add(message.Message.Subject ?? string.Empty);

        await listener.StartAsync();

        await listener.WaitForDelayAsync();
        listener.SetMessages(new TestPop3PollListener.TestMessage("uid2", CreateMessage("Second")));
        listener.ReleaseNextDelay();

        await listener.WaitForDelayAsync();
        listener.SetMessages(
            new TestPop3PollListener.TestMessage("uid2", CreateMessage("Second")),
            new TestPop3PollListener.TestMessage("uid3", CreateMessage("Third")));
        listener.ReleaseNextDelay();

        await listener.WaitForDelayAsync();

        await listener.StopAsync();

        Assert.Single(receivedSubjects);
        Assert.Equal("Third", receivedSubjects[0]);
    }

    [Fact]
    public async Task StopAsync_WaitsForPollingLoopToComplete() {
        var listener = new TestPop3PollListener { DelayIgnoresCancellation = true };

        await listener.StartAsync();
        await listener.WaitForDelayAsync();

        var stopTask = listener.StopAsync();

        var completed = await Task.WhenAny(stopTask, Task.Delay(100));
        Assert.NotSame(stopTask, completed);

        listener.ReleaseNextDelay();

        await stopTask;

        var cancelField = typeof(Pop3PollListener).GetField("_cancel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pollingTaskField = typeof(Pop3PollListener).GetField("_pollingTask", BindingFlags.NonPublic | BindingFlags.Instance)!;

        Assert.Null(cancelField.GetValue(listener));
        Assert.Null(pollingTaskField.GetValue(listener));
    }

    [Fact]
    public async Task PollLoop_RaisesPollErrorAndRecoversAfterException() {
        var listener = new TestPop3PollListener();
        listener.SetMessages(new TestPop3PollListener.TestMessage("uid1", CreateMessage("Initial")));
        var receivedSubjects = new List<string>();
        var errors = new List<Exception>();
        listener.MessageArrived += (_, message) => receivedSubjects.Add(message.Message.Subject ?? string.Empty);
        listener.PollError += (_, exception) => errors.Add(exception);

        await listener.StartAsync();
        await listener.WaitForDelayAsync();

        listener.SetMessages(
            new TestPop3PollListener.TestMessage("uid1", CreateMessage("Initial")),
            new TestPop3PollListener.TestMessage("uid2", CreateMessage("New")));
        listener.ThrowOnNextFetch(new InvalidOperationException("Boom"));
        listener.ReleaseNextDelay();

        await listener.WaitForDelayAsync();
        listener.ReleaseNextDelay();

        await listener.WaitForDelayAsync();
        listener.SetMessages(
            new TestPop3PollListener.TestMessage("uid1", CreateMessage("Initial")),
            new TestPop3PollListener.TestMessage("uid2", CreateMessage("New")));
        listener.ReleaseNextDelay();

        await listener.WaitForDelayAsync();

        await listener.StopAsync();

        Assert.Single(errors);
        Assert.IsType<InvalidOperationException>(errors[0]);
        Assert.Single(receivedSubjects);
        Assert.Equal("New", receivedSubjects[0]);
    }

    [Fact]
    public async Task StopAsync_DuringErrorBackoff_CompletesPollingTaskSuccessfully() {
        var listener = new TestPop3PollListener();
        listener.SetMessages(new TestPop3PollListener.TestMessage("uid1", CreateMessage("Initial")));

        await listener.StartAsync();
        await listener.WaitForDelayAsync();

        listener.SetMessages(
            new TestPop3PollListener.TestMessage("uid1", CreateMessage("Initial")),
            new TestPop3PollListener.TestMessage("uid2", CreateMessage("New")));
        listener.ThrowOnNextFetch(new InvalidOperationException("Boom"));
        listener.ReleaseNextDelay();

        await listener.WaitForDelayAsync();

        var pollingTaskField = typeof(Pop3PollListener).GetField("_pollingTask", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pollingTask = (Task)pollingTaskField.GetValue(listener)!;

        await listener.StopAsync();

        Assert.Equal(TaskStatus.RanToCompletion, pollingTask.Status);
        Assert.Null(pollingTaskField.GetValue(listener));
    }

    private static MimeMessage CreateMessage(string subject) {
        var message = new MimeMessage();
        message.Subject = subject;
        return message;
    }

    private sealed class TestPop3PollListener : Pop3PollListener {
        private readonly object _syncRoot = new object();
        private readonly List<TestMessage> _messages = new List<TestMessage>();
        private readonly Queue<TaskCompletionSource<bool>> _pendingDelays = new Queue<TaskCompletionSource<bool>>();
        private readonly SemaphoreSlim _delayScheduled = new SemaphoreSlim(0);
        private Exception? _nextFetchException;

        public TestPop3PollListener()
            : base(new Pop3Client(), TimeSpan.Zero) {
        }

        public bool DelayIgnoresCancellation { get; set; }

        public void SetMessages(params TestMessage[] messages) {
            lock (_syncRoot) {
                _messages.Clear();
                if (messages != null && messages.Length > 0) {
                    _messages.AddRange(messages);
                }
            }
        }

        public Task WaitForDelayAsync() => _delayScheduled.WaitAsync();

        public void ReleaseNextDelay() {
            TaskCompletionSource<bool>? pending = null;
            lock (_syncRoot) {
                if (_pendingDelays.Count > 0) {
                    pending = _pendingDelays.Dequeue();
                }
            }

            pending?.TrySetResult(true);
        }

        public void ThrowOnNextFetch(Exception exception) {
            if (exception == null) {
                throw new ArgumentNullException(nameof(exception));
            }

            lock (_syncRoot) {
                _nextFetchException = exception;
            }
        }

        protected override int GetMessageCount() {
            lock (_syncRoot) {
                return _messages.Count;
            }
        }

        protected override Task<string> GetMessageUidAsync(int index, CancellationToken cancellationToken) {
            lock (_syncRoot) {
                return Task.FromResult(_messages[index].Uid);
            }
        }

        protected override Task<MimeMessage> GetMessageAsync(int index, CancellationToken cancellationToken) {
            lock (_syncRoot) {
                if (_nextFetchException != null) {
                    var exception = _nextFetchException;
                    _nextFetchException = null;
                    throw exception;
                }

                return Task.FromResult(_messages[index].Message);
            }
        }

        protected override Task NoOpAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!DelayIgnoresCancellation && cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            }

            lock (_syncRoot) {
                _pendingDelays.Enqueue(tcs);
            }

            _delayScheduled.Release();

            return tcs.Task;
        }

        public readonly struct TestMessage {
            public TestMessage(string uid, MimeMessage message) {
                Uid = uid;
                Message = message;
            }

            public string Uid { get; }

            public MimeMessage Message { get; }
        }
    }
}