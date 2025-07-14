using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MimeKit;
using MailKit;
using Xunit;

namespace Mailozaurr.Tests;

public class AdditionalCoverageTests
{
    private class FakeFolder : MessageFlagSetter.IImapFolder
    {
        public bool AddCalled;
        public bool RemoveCalled;
        public Task AddFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, System.Threading.CancellationToken cancellationToken = default)
        {
            AddCalled = true;
            return Task.CompletedTask;
        }
        public Task RemoveFlagsAsync(UniqueId uid, MessageFlags flags, bool silent, System.Threading.CancellationToken cancellationToken = default)
        {
            RemoveCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task MessageFlagSetter_AddOrRemoveFlags()
    {
        var folder = new FakeFolder();
        var uid = new UniqueId(1);
        await MessageFlagSetter.SetFlagsAsync(folder, uid, MessageFlags.Seen, true);
        Assert.True(folder.AddCalled);
        await MessageFlagSetter.SetFlagsAsync(folder, uid, MessageFlags.Seen, false);
        Assert.True(folder.RemoveCalled);
    }

    [Fact]
    public async Task MessageFlagSetter_Pop3ReadState()
    {
        var client = new MailKit.Net.Pop3.Pop3Client();
        await MessageFlagSetter.SetReadAsync(client, 5, true);
        Assert.True(MessageFlagSetter.TryGetPop3Read(client, 5, out var read) && read);
    }

    [Fact]
    public void MimeKitUtils_SavesAttachments()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var part = new MimePart("text/plain")
        {
            Content = new MimeContent(new MemoryStream(new byte[] {1,2,3})),
            FileName = "a.txt"
        };
        var msg = new MimeMessage();
        var msgPart = new MessagePart { Message = msg };
        MimeKitUtils.SaveAttachments(new MimeEntity[] { part, msgPart }, dir);
        var files = Directory.GetFiles(dir);
        Assert.Equal(2, files.Length);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void EmailMessage_ConvertMissingFiles()
    {
        var tmpDir = Path.GetTempPath();
        var eml = new FileInfo(Path.Combine(tmpDir, "missing.eml"));
        var msg = new FileInfo(Path.Combine(tmpDir, "out.msg"));
        var res1 = EmailMessage.ConvertEmlToMsg(eml, msg, false);
        Assert.False(res1.Status);
        Assert.Contains("does not exist", res1.Error, StringComparison.OrdinalIgnoreCase);

        var res2 = EmailMessage.ConvertMsgToEml(msg, eml, false);
        Assert.False(res2.Status);
        Assert.Contains("does not exist", res2.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoggingMessages_PropertyDelegates()
    {
        LoggingMessages.Verbose = true;
        Assert.True(LoggingMessages.Logger.IsVerbose);
        LoggingMessages.Verbose = false;
        Assert.False(LoggingMessages.Logger.IsVerbose);
    }

    [Fact]
    public void InternalLogger_WarningEventRaised()
    {
        var logger = new InternalLogger { IsWarning = true };
        string? msg = null;
        logger.OnWarningMessage += (_, e) => msg = e.Message;
        logger.WriteWarning("warn");
        Assert.Equal("warn", msg);
    }
}
