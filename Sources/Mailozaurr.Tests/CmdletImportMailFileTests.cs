using Mailozaurr.PowerShell;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class CmdletImportMailFileTests {
    [Fact]
    public void ProcessRecordAsync_EmptyPath_Warns() {
        var cmdlet = new CmdletImportMailFile { InputPath = " " };

        var (outputs, warnings, errors) = InvokeAndCapture(cmdlet);

        Assert.Empty(outputs);
        Assert.Empty(errors);
        Assert.Single(warnings);
        Assert.Contains("File path is empty", warnings[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessRecordAsync_UnsupportedExtension_WritesActionableError() {
        var tempDir = CreateTempDirectory();
        try {
            var txtPath = Path.Combine(tempDir, "sample.txt");
            File.WriteAllText(txtPath, "data");
            var cmdlet = new CmdletImportMailFile { InputPath = txtPath };

            var (outputs, warnings, errors) = InvokeAndCapture(cmdlet);

            Assert.Empty(outputs);
            Assert.Empty(warnings);
            Assert.Single(errors);
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

            var (outputs, warnings, errors) = InvokeAndCapture(cmdlet);

            Assert.Empty(warnings);
            Assert.Empty(errors);
            Assert.Single(outputs);
            var message = Assert.IsType<MailFileMessage>(outputs[0]);
            Assert.Equal(MailFileFormat.Eml, message.Format);
            Assert.Equal("Cmdlet subject", message.Subject);
        } finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void VerifySignature_IsExposedAsPowerShellParameter() {
        PropertyInfo property = typeof(CmdletImportMailFile).GetProperty(
            nameof(CmdletImportMailFile.VerifySignature))!;

        Assert.Equal(typeof(SwitchParameter), property.PropertyType);
        Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
    }

    private static (List<object?> Outputs, List<string> Warnings, List<ErrorRecord> Errors) InvokeAndCapture(
        CmdletImportMailFile cmdlet) {
        var asyncType = typeof(AsyncPSCmdlet);
        var pipelineItemType = asyncType.GetNestedType("PipelineItem", BindingFlags.NonPublic)!;
        var outPipeType = typeof(BlockingCollection<>).MakeGenericType(pipelineItemType);

        var outPipe = Activator.CreateInstance(outPipeType)!;

        var outPipeField = asyncType.GetField("_currentOutPipe", BindingFlags.NonPublic | BindingFlags.Instance)!;
        outPipeField.SetValue(cmdlet, outPipe);

        var method = typeof(CmdletImportMailFile).GetMethod("ProcessRecordAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(cmdlet, null)!;
        task.GetAwaiter().GetResult();

        var outputs = new List<object?>();
        var warnings = new List<string>();
        var errors = new List<ErrorRecord>();
        var items = (Array)outPipeType.GetMethod("ToArray")!.Invoke(outPipe, null)!;
        var valueProperty = pipelineItemType.GetProperty("Value")!;
        var typeProperty = pipelineItemType.GetProperty("Type")!;

        foreach (var item in items) {
            var data = valueProperty.GetValue(item);
            var pipeline = typeProperty.GetValue(item)?.ToString();
            if (string.Equals(pipeline, "Output", StringComparison.Ordinal) || string.Equals(pipeline, "OutputEnumerate", StringComparison.Ordinal)) {
                outputs.Add(data);
            } else if (string.Equals(pipeline, "Warning", StringComparison.Ordinal)) {
                warnings.Add(data?.ToString() ?? string.Empty);
            } else if (string.Equals(pipeline, "Error", StringComparison.Ordinal)) {
                errors.Add(Assert.IsType<ErrorRecord>(data));
            }
        }

        outPipeField.SetValue(cmdlet, null);

        return (outputs, warnings, errors);
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
