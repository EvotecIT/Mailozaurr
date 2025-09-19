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
        var cts = new CancellationTokenSource();
        field.SetValue(listener, cts);

        listener.Dispose();

        Assert.Null(field.GetValue(listener));
        Assert.Throws<ObjectDisposedException>(() => _ = cts.Token.WaitHandle);
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

        listener.Stop();

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

        listener.Stop();

        Assert.Single(receivedSubjects);
        Assert.Equal("Third", receivedSubjects[0]);
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

        public TestPop3PollListener()
            : base(new Pop3Client(), TimeSpan.Zero) {
        }

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
                return Task.FromResult(_messages[index].Message);
            }
        }

        protected override Task NoOpAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (cancellationToken.CanBeCanceled) {
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