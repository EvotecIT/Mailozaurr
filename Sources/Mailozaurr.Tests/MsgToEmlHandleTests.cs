using System.IO;
using Xunit;

namespace Mailozaurr.Tests;

public class MsgToEmlHandleTests
{
    [Fact]
    public void ConvertFromMsgToEml_ReleasesFileHandle()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var emlFile = Path.Combine(tempDir, "mail.eml");
        var msgFile = Path.Combine(tempDir, "mail.msg");
        var outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(outputDir);

        var message = new MimeKit.MimeMessage();
        message.Subject = "test";
        message.Body = new MimeKit.TextPart("plain") { Text = "body" };
        message.WriteTo(emlFile);

        Mailozaurr.EmailMessage.ConvertEmlToMsg(new FileInfo(emlFile), new FileInfo(msgFile), true);

        var cmd = new Mailozaurr.PowerShell.CmdletConvertFromMsgToEml
        {
            InputPath = new[] { msgFile },
            OutputFolder = outputDir,
            Force = true
        };

        var type = typeof(Mailozaurr.PowerShell.CmdletConvertFromMsgToEml);
        type.GetMethod("BeginProcessing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(cmd, null);
        type.GetMethod("ProcessRecord", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(cmd, null);
        type.GetMethod("EndProcessing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(cmd, null);

        File.Delete(msgFile);
        Assert.False(File.Exists(msgFile));

        Directory.Delete(tempDir, true);
    }
}
