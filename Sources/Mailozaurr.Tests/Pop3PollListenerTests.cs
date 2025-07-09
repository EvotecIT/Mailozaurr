using MailKit.Net.Pop3;
using System;
using System.Reflection;
using System.Threading;
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
}