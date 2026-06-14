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
}