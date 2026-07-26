using MailKit.Net.Imap;
using Mailozaurr.PowerShell;
using System;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class CmdletWaitImapMessageTests {
    [Fact]
    public void EndProcessing_DisposesListenerAndDetachesEvents() {
        var cmd = new CmdletWaitIMAPMessage();
        var listener = new ImapIdleListener(new ImapClient());
        var field = typeof(CmdletWaitIMAPMessage).GetField("_listener", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(cmd, listener);

        var method = typeof(CmdletWaitIMAPMessage).GetMethod("OnMessageArrived", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var handler = (EventHandler<ImapEmailMessage>)Delegate.CreateDelegate(typeof(EventHandler<ImapEmailMessage>), cmd, method);
        listener.MessageArrived += handler;

        typeof(CmdletWaitIMAPMessage).GetMethod("EndProcessing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cmd, null);

        var eventField = typeof(ImapIdleListener).GetField("MessageArrived", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Null(eventField.GetValue(listener));
    }

    [Theory]
    [InlineData(typeof(CmdletWaitIMAPMessage))]
    [InlineData(typeof(CmdletWaitPOP3Message))]
    [InlineData(typeof(CmdletWaitGraphMessage))]
    public void RecordCleanupDisposesAndClearsCancellationSources(
        Type cmdletType) {

        object cmdlet = Activator.CreateInstance(cmdletType)!;
        string[] fieldNames = {
            "_linkedSource",
            "_timeoutSource",
            "_matchSource"
        };
        var sources = new List<CancellationTokenSource>();
        foreach (string fieldName in fieldNames) {
            var source = new CancellationTokenSource();
            sources.Add(source);
            cmdletType.GetField(
                    fieldName,
                    BindingFlags.NonPublic |
                    BindingFlags.Instance)!
                .SetValue(
                    cmdlet,
                    source);
        }

        MethodInfo cleanup = cmdletType.GetMethod(
            "DisposeRecordResources",
            BindingFlags.NonPublic |
            BindingFlags.Instance)!;
        cleanup.Invoke(cmdlet, null);
        cleanup.Invoke(cmdlet, null);

        foreach (string fieldName in fieldNames) {
            Assert.Null(
                cmdletType.GetField(
                        fieldName,
                        BindingFlags.NonPublic |
                        BindingFlags.Instance)!
                    .GetValue(cmdlet));
        }
        foreach (CancellationTokenSource source in sources) {
            Assert.Throws<ObjectDisposedException>(
                source.Cancel);
        }
    }

    [Fact]
    public async Task GraphStopAndRecordCleanupCanRunConcurrently() {
        for (var attempt = 0; attempt < 100; attempt++) {
            var cmdlet = new CmdletWaitGraphMessage();
            var cmdletType = cmdlet.GetType();
            foreach (string fieldName in new[] { "_linkedSource", "_timeoutSource", "_matchSource" }) {
                cmdletType.GetField(
                        fieldName,
                        BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(cmdlet, new CancellationTokenSource());
            }

            MethodInfo cleanup = cmdletType.GetMethod(
                "DisposeRecordResources",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo stop = cmdletType.GetMethod(
                "StopProcessing",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            using var start = new ManualResetEventSlim();

            Task cleanupTask = Task.Run(() => {
                start.Wait();
                cleanup.Invoke(cmdlet, null);
            });
            Task stopTask = Task.Run(() => {
                start.Wait();
                stop.Invoke(cmdlet, null);
            });
            start.Set();

            await Task.WhenAll(cleanupTask, stopTask);
            cmdlet.Dispose();
        }
    }
}
