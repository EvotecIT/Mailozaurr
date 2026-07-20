using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Mailozaurr.Tests;

public class MailFileReaderTests {
    [Fact]
    public void ReadEml_MapsBasicFieldsAndHeaders() {
        var tempDir = CreateTempDirectory();
        try {
            var emlPath = CreateEmlFile(tempDir, "sample.eml", "Test subject", "Hello", "X-Mailozaurr-Test: Value");
            var options = new MailFileReaderOptions {
                IncludeHeaders = true,
                IncludeAttachments = false,
                IncludeAttachmentContent = false
            };

            var message = MailFileReader.Read(emlPath, options);

            Assert.Equal(MailFileFormat.Eml, message.Format);
            Assert.Equal("Test subject", message.Subject);
            Assert.Equal("alice@example.com", message.From?.Address);
            Assert.Single(message.To);
            Assert.Equal("bob@example.com", message.To[0].Address);
            Assert.Contains("Hello", message.BodyText ?? string.Empty);
            Assert.NotNull(message.Headers);
            Assert.True(message.Headers!.TryGetValue("X-Mailozaurr-Test", out var headerValue));
            Assert.Contains("Value", headerValue);
        } finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryRead_MissingFile_ReturnsFalse() {
        var tempDir = CreateTempDirectory();
        try {
            var missingPath = Path.Combine(tempDir, "missing.eml");
            var result = MailFileReader.TryRead(missingPath, out var message, out var error);

            Assert.False(result);
            Assert.Null(message);
            Assert.NotNull(error);
            Assert.Contains("doesn't exist", error!, StringComparison.OrdinalIgnoreCase);
        } finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryRead_UnsupportedExtension_ReturnsFalse() {
        var tempDir = CreateTempDirectory();
        try {
            var txtPath = Path.Combine(tempDir, "sample.txt");
            File.WriteAllText(txtPath, "data");

            var result = MailFileReader.TryRead(txtPath, out var message, out var error);

            Assert.False(result);
            Assert.Null(message);
            Assert.NotNull(error);
            Assert.Contains("not a supported", error!, StringComparison.OrdinalIgnoreCase);
        } finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryRead_ErrorDiagnostic_ReturnsFalseAndDiagnosticCode() {
        var tempDir = CreateTempDirectory();
        try {
            string emlPath = Path.Combine(tempDir, "invalid.eml");
            File.WriteAllText(emlPath,
                "From: a@example.com\r\nTo: b@example.com\r\nSubject: Invalid\r\n" +
                "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n" +
                "Content-Transfer-Encoding: base64\r\n\r\n!!!!");

            bool result = MailFileReader.TryRead(emlPath, out MailFileMessage? message, out string? error);

            Assert.False(result);
            Assert.Null(message);
            Assert.Contains("EMAIL_MIME_BASE64_INVALID", error, StringComparison.Ordinal);
        } finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReadMsg_FromConvertedEml_MapsFields() {
        var tempDir = CreateTempDirectory();
        var outputDir = CreateTempDirectory();
        try {
            var emlPath = CreateEmlFile(tempDir, "sample.eml", "Msg subject", "Hello from msg");
            var msgPath = Path.Combine(outputDir, "sample.msg");

            var conversion = EmailMessage.ConvertEmlToMsg(new FileInfo(emlPath), new FileInfo(msgPath), true);
            Assert.True(conversion.Status);
            Assert.True(File.Exists(msgPath));

            var message = MailFileReader.Read(msgPath);

            Assert.Equal(MailFileFormat.Msg, message.Format);
            Assert.Equal("Msg subject", message.Subject);
            Assert.Equal("alice@example.com", message.From?.Address);
            Assert.Single(message.To);
            Assert.Equal("bob@example.com", message.To[0].Address);
        } finally {
            Directory.Delete(tempDir, true);
            Directory.Delete(outputDir, true);
        }
    }

    private static string CreateTempDirectory() {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateEmlFile(string directory, string fileName, string subject, string body, string? extraHeader = null) {
        var lines = new[] {
            "From: Alice <alice@example.com>",
            "To: Bob <bob@example.com>",
            $"Subject: {subject}",
            "Date: Mon, 21 Jun 2021 10:00:00 +0000",
            "Message-ID: <test@example.com>",
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=utf-8"
        };

        var headerBlock = string.Join("\r\n", lines);
        if (!string.IsNullOrWhiteSpace(extraHeader)) {
            headerBlock = string.Concat(headerBlock, "\r\n", extraHeader);
        }

        var content = string.Concat(headerBlock, "\r\n\r\n", body);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
