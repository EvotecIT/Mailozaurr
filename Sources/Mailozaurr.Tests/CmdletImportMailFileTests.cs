using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Mailozaurr.PowerShell;
using Xunit;

namespace Mailozaurr.Tests;

public class CmdletImportMailFileTests {
    [Fact]
    public void ProcessRecordAsync_EmptyPath_Warns() {
        var cmdlet = new CmdletImportMailFile { InputPath = " " };

        var (outputs, warnings) = InvokeAndCapture(cmdlet);

        Assert.Empty(outputs);
        Assert.Single(warnings);
        Assert.Contains("File path is empty", warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessRecordAsync_UnsupportedExtension_Warns() {
        var tempDir = CreateTempDirectory();
        try {
            var txtPath = Path.Combine(tempDir, "sample.txt");
            File.WriteAllText(txtPath, "data");
            var cmdlet = new CmdletImportMailFile { InputPath = txtPath };

            var (outputs, warnings) = InvokeAndCapture(cmdlet);

            Assert.Empty(outputs);
            Assert.Single(warnings);
            Assert.Contains("not a .msg or .eml", warnings[0], StringComparison.OrdinalIgnoreCase);
        } finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProcessRecordAsync_ValidEml_OutputsMessage() {
        var tempDir = CreateTempDirectory();
        try {
            var emlPath = CreateEmlFile(tempDir, "sample.eml", "Cmdlet subject", "Hello");
            var cmdlet = new CmdletImportMailFile { InputPath = emlPath };

            var (outputs, warnings) = InvokeAndCapture(cmdlet);

            Assert.Empty(warnings);
            Assert.Single(outputs);
            var message = Assert.IsType<MailFileMessage>(outputs[0]);
            Assert.Equal(MailFileFormat.Eml, message.Format);
            Assert.Equal("Cmdlet subject", message.Subject);
        } finally {
            Directory.Delete(tempDir, true);
        }
    }

    private static (List<object?> Outputs, List<string> Warnings) InvokeAndCapture(CmdletImportMailFile cmdlet) {
        var asyncType = typeof(AsyncPSCmdlet);
        var pipelineType = asyncType.GetNestedType("PipelineType", BindingFlags.NonPublic)!;
        var tupleType = typeof(ValueTuple<,>).MakeGenericType(typeof(object), pipelineType);
        var outPipeType = typeof(BlockingCollection<>).MakeGenericType(tupleType);

        var outPipe = Activator.CreateInstance(outPipeType)!;
        var replyPipe = new BlockingCollection<object?>();

        var outPipeField = asyncType.GetField("_currentOutPipe", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var replyPipeField = asyncType.GetField("_currentReplyPipe", BindingFlags.NonPublic | BindingFlags.Instance)!;
        outPipeField.SetValue(cmdlet, outPipe);
        replyPipeField.SetValue(cmdlet, replyPipe);

        var method = typeof(CmdletImportMailFile).GetMethod("ProcessRecordAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(cmdlet, null)!;
        task.GetAwaiter().GetResult();

        var outputs = new List<object?>();
        var warnings = new List<string>();
        var items = (Array)outPipeType.GetMethod("ToArray")!.Invoke(outPipe, null)!;
        var item1Field = tupleType.GetField("Item1")!;
        var item2Field = tupleType.GetField("Item2")!;

        foreach (var item in items) {
            var data = item1Field.GetValue(item);
            var pipeline = item2Field.GetValue(item)?.ToString();
            if (string.Equals(pipeline, "Output", StringComparison.Ordinal) || string.Equals(pipeline, "OutputEnumerate", StringComparison.Ordinal)) {
                outputs.Add(data);
            } else if (string.Equals(pipeline, "Warning", StringComparison.Ordinal)) {
                warnings.Add(data?.ToString() ?? string.Empty);
            }
        }

        outPipeField.SetValue(cmdlet, null);
        replyPipeField.SetValue(cmdlet, null);

        return (outputs, warnings);
    }

    private static string CreateTempDirectory() {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateEmlFile(string directory, string fileName, string subject, string body) {
        var content = string.Join("\r\n", new[] {
            "From: Alice <alice@example.com>",
            "To: Bob <bob@example.com>",
            $"Subject: {subject}",
            "Date: Mon, 21 Jun 2021 10:00:00 +0000",
            "Message-ID: <test@example.com>",
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=utf-8",
            string.Empty,
            body
        });

        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
