using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Mailozaurr.Tests;

public class EmailMessageConversionTests {
    [Fact]
    public void ConvertEmlToMsg_CreatesOutputDirectory() {
        var emlDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(emlDir);
        var emlPath = Path.Combine(emlDir, "sample.eml");
        File.WriteAllText(emlPath, "From: a@example.com\r\nTo: a@example.com\r\nSubject: Test\r\nDate: Mon, 21 Jun 2021 10:00:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nHello");

        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var results = EmailMessage.ConvertEmlToMsg(new[] { emlPath }, outputDir, true).ToList();
        Assert.True(results[0].Status);
        Assert.True(Directory.Exists(outputDir));
        Assert.True(File.Exists(Path.Combine(outputDir, "sample.msg")));

        Directory.Delete(emlDir, true);
        Directory.Delete(outputDir, true);
    }

    [Fact]
    public void ConvertEmlToMsg_MultipleFiles() {
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);

        var eml1 = Path.Combine(tmpDir, "a.eml");
        File.WriteAllText(eml1, "From: a@example.com\r\nTo: a@example.com\r\nSubject: A\r\nDate: Mon, 21 Jun 2021 10:00:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nHello");
        var eml2 = Path.Combine(tmpDir, "b.eml");
        File.WriteAllText(eml2, "From: b@example.com\r\nTo: b@example.com\r\nSubject: B\r\nDate: Mon, 21 Jun 2021 10:00:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nWorld");

        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var results = EmailMessage.ConvertEmlToMsg(new[] { eml1, eml2 }, outputDir, true).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Status));
        Assert.True(File.Exists(Path.Combine(outputDir, "a.msg")));
        Assert.True(File.Exists(Path.Combine(outputDir, "b.msg")));

        Directory.Delete(tmpDir, true);
        Directory.Delete(outputDir, true);
    }

    [Fact]
    public void ConvertMsgToEml_CreatesOutputDirectory() {
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        var emlPath = Path.Combine(tmpDir, "sample.eml");
        File.WriteAllText(emlPath, "From: a@example.com\r\nTo: a@example.com\r\nSubject: Test\r\nDate: Mon, 21 Jun 2021 10:00:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nHello");

        var msgDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(msgDir);
        EmailMessage.ConvertEmlToMsg(new[] { emlPath }, msgDir, true).ToList();
        var msgPath = Path.Combine(msgDir, "sample.msg");

        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Assert.Throws<NotImplementedException>(() => EmailMessage.ConvertMsgToEml(new[] { msgPath }, outputDir, true).ToList());
        Assert.True(Directory.Exists(outputDir));


        Directory.Delete(tmpDir, true);
        Directory.Delete(msgDir, true);
        Directory.Delete(outputDir, true);
    }

    [Fact]
    public void ConvertEmlToMsg_DoesNotOverwriteWhenForceFalse() {
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        Directory.CreateDirectory(outputDir);
        try {
            var emlPath = Path.Combine(tmpDir, "sample.eml");
            File.WriteAllText(emlPath, "From: a@example.com\r\nTo: a@example.com\r\nSubject: Test\r\nDate: Mon, 21 Jun 2021 10:00:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nHello");
            var msgPath = Path.Combine(outputDir, "sample.msg");
            File.WriteAllText(msgPath, "sentinel");

            var result = EmailMessage.ConvertEmlToMsg(new FileInfo(emlPath), new FileInfo(msgPath), false);

            Assert.False(result.Status);
            Assert.NotNull(result.Error);
            Assert.Contains("already exists", result.Error!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("sentinel", File.ReadAllText(msgPath));
        } finally {
            Directory.Delete(tmpDir, true);
            Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void ConvertEmlToMsg_OverwritesWhenForceTrue() {
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);
        Directory.CreateDirectory(outputDir);
        try {
            var emlPath = Path.Combine(tmpDir, "sample.eml");
            File.WriteAllText(emlPath, "From: a@example.com\r\nTo: a@example.com\r\nSubject: Test\r\nDate: Mon, 21 Jun 2021 10:00:00 +0000\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nHello");
            var msgPath = Path.Combine(outputDir, "sample.msg");
            File.WriteAllText(msgPath, "sentinel");
            var sentinelLength = new FileInfo(msgPath).Length;

            var result = EmailMessage.ConvertEmlToMsg(new FileInfo(emlPath), new FileInfo(msgPath), true);

            Assert.True(result.Status);
            Assert.True(File.Exists(msgPath));
            Assert.True(new FileInfo(msgPath).Length > sentinelLength);
        } finally {
            Directory.Delete(tmpDir, true);
            Directory.Delete(outputDir, true);
        }
    }
}
