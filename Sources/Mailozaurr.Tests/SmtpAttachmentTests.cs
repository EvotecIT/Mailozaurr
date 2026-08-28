using Mailozaurr.Definitions;
using MimeKit;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAttachmentTests {
    [Fact]
    public void CreateMessage_MissingAttachment_Throws() {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        if (File.Exists(path)) File.Delete(path);
        var smtp = new Smtp {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor(path) }
        };

        var exception = Assert.Throws<FileNotFoundException>(() => smtp.CreateMessage());
        Assert.Equal(path, exception.FileName);
    }

    [Fact]
    public void CreateMessage_StreamAttachmentDescriptor_AddsAttachment() {
        var data = Encoding.UTF8.GetBytes("hello world");
        using var source = new MemoryStream(data);
        var descriptor = new StreamAttachmentDescriptor(source, "greeting.txt") {
            ContentType = "text/plain",
            Headers = new Dictionary<string, string> { { "X-Test", "Stream" } },
        };

        var smtp = new Smtp {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { descriptor },
        };

        smtp.CreateMessage();

        var multipart = Assert.IsType<Multipart>(smtp.Message.Body);
        var part = Assert.Single(multipart.OfType<MimePart>(), p => p.IsAttachment);

        Assert.Equal("greeting.txt", part.FileName);
        Assert.Equal("text/plain", part.ContentType.MimeType);
        Assert.Equal("Stream", part.Headers["X-Test"]);

        using var extracted = new MemoryStream();
        var content = Assert.IsAssignableFrom<IMimeContent>(part.Content);
        content.DecodeTo(extracted);
        Assert.Equal(data, extracted.ToArray());

        Assert.True(source.CanRead);
    }

    [Fact]
    public void CreateMessage_RelativeAndAbsoluteAttachmentPaths_AddsFileOnce() {
        var fileName = $"mailozaurr-smtp-{System.Guid.NewGuid():N}.tmp";
        var absolutePath = Path.Combine(System.Environment.CurrentDirectory, fileName);
        File.WriteAllText(absolutePath, "data");
        var smtp = new Smtp {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> {
                new FileAttachmentDescriptor(fileName),
                new FileAttachmentDescriptor(absolutePath),
            },
        };

        try {
            smtp.CreateMessage();
            var multipart = Assert.IsType<Multipart>(smtp.Message.Body);

            Assert.Single(multipart.OfType<MimePart>(), part => part.IsAttachment);
        } finally {
            File.Delete(absolutePath);
        }
    }

    [Fact]
    public void StreamAttachmentDescriptor_ClosedSourceRemainsReusable() {
        var bytes = Encoding.UTF8.GetBytes("repeatable");
        var source = new MemoryStream(bytes);
        var descriptor = new StreamAttachmentDescriptor(source, "repeatable.txt", leaveStreamOpen: false);

        var first = descriptor.GetContentBytes();
        var second = descriptor.GetContentBytes();

        Assert.Equal(bytes, first);
        Assert.Equal(bytes, second);
        Assert.False(source.CanRead);
    }

    [Fact]
    public void StreamAttachmentDescriptor_StagesLargeContentAndDeletesItOnDispose() {
        string directory = Path.Combine(Path.GetTempPath(), "MailozaurrStage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var source = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
            var descriptor = new StreamAttachmentDescriptor(
                source,
                "staged.bin",
                stagingOptions: new AttachmentStreamStagingOptions {
                    MemoryThresholdBytes = 2,
                    MaxBytes = 10,
                    TempDirectory = directory
                });

            using (Stream stream = descriptor.OpenContentStream()) {
                Assert.IsType<FileStream>(stream);
                Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, ReadAll(stream));
            }
            Assert.Single(Directory.GetFiles(directory));

            descriptor.Dispose();

            Assert.Empty(Directory.GetFiles(directory));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SmtpDispose_ReleasesStreamAttachmentStagingOwnedByTheSendOperation() {
        string directory = Path.Combine(Path.GetTempPath(), "MailozaurrStage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var descriptor = new StreamAttachmentDescriptor(
                new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
                "staged.bin",
                stagingOptions: new AttachmentStreamStagingOptions {
                    MemoryThresholdBytes = 2,
                    MaxBytes = 10,
                    TempDirectory = directory
                });
            var smtp = new Smtp {
                Attachments = new List<AttachmentDescriptor> { descriptor }
            };
            descriptor.GetContentBytes();
            Assert.Single(Directory.GetFiles(directory));

            smtp.Dispose();

            Assert.Empty(Directory.GetFiles(directory));
            Assert.Throws<ObjectDisposedException>(() => descriptor.OpenContentStream());
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SmtpSend_ReleasesStreamAttachmentStagingOnCompletion() {
        string directory = Path.Combine(Path.GetTempPath(), "MailozaurrStage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var descriptor = new StreamAttachmentDescriptor(
                new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
                "staged.bin",
                stagingOptions: new AttachmentStreamStagingOptions {
                    MemoryThresholdBytes = 2,
                    MaxBytes = 10,
                    TempDirectory = directory
                });
            var smtp = new Smtp {
                DryRun = true,
                Attachments = new List<AttachmentDescriptor> { descriptor }
            };
            try {
                descriptor.GetContentBytes();

                smtp.Send();

                Assert.Empty(Directory.GetFiles(directory));
                Assert.Throws<ObjectDisposedException>(() => descriptor.OpenContentStream());
            } finally {
                smtp.Dispose();
            }
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SmtpSend_RetainsStagingOnlyWhenCallerExplicitlyOwnsCleanup() {
        string directory = Path.Combine(Path.GetTempPath(), "MailozaurrStage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var descriptor = new StreamAttachmentDescriptor(
            new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
            "staged.bin",
            stagingOptions: new AttachmentStreamStagingOptions {
                MemoryThresholdBytes = 2,
                MaxBytes = 10,
                TempDirectory = directory,
                RetainStagedContentAfterSend = true
            });
        try {
            var smtp = new Smtp {
                DryRun = true,
                Attachments = new List<AttachmentDescriptor> { descriptor }
            };
            try {
                descriptor.GetContentBytes();

                smtp.Send();

                Assert.Single(Directory.GetFiles(directory));
                Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, descriptor.GetContentBytes());
            } finally {
                smtp.Dispose();
            }
        } finally {
            descriptor.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void StreamAttachmentDescriptor_RejectsContentPastItsBoundAndCleansStaging() {
        string directory = Path.Combine(Path.GetTempPath(), "MailozaurrStage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            using var descriptor = new StreamAttachmentDescriptor(
                new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
                "too-large.bin",
                stagingOptions: new AttachmentStreamStagingOptions {
                    MemoryThresholdBytes = 2,
                    MaxBytes = 4,
                    TempDirectory = directory
                });

            Assert.Throws<InvalidDataException>(() => descriptor.OpenContentStream());
            Assert.Empty(Directory.GetFiles(directory));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] ReadAll(Stream stream) {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    [Fact]
    public void CreateMessage_ByteArrayAttachmentDescriptor_AddsAttachment() {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var descriptor = new ByteArrayAttachmentDescriptor(data, "data.bin") {
            ContentType = "application/octet-stream",
        };

        var smtp = new Smtp {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { descriptor },
        };

        smtp.CreateMessage();

        var multipart = Assert.IsType<Multipart>(smtp.Message.Body);
        var part = Assert.Single(multipart.OfType<MimePart>(), p => p.IsAttachment);

        Assert.Equal("data.bin", part.FileName);
        Assert.Equal("application/octet-stream", part.ContentType.MimeType);

        using var extracted = new MemoryStream();
        var content = Assert.IsAssignableFrom<IMimeContent>(part.Content);
        content.DecodeTo(extracted);
        Assert.Equal(data, extracted.ToArray());
    }

    [Fact]
    public void CreateMessage_BinaryAttachment_UsesBase64EncodingToAvoidBareLineFeeds() {
        var data = new byte[] { 0x50, 0x4b, 0x03, 0x04, 0x0a, 0xff, 0x00, 0x0a, 0x7f };
        var descriptor = new ByteArrayAttachmentDescriptor(data, "document.docx") {
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        };

        var smtp = new Smtp {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { descriptor },
        };

        smtp.CreateMessage();

        var multipart = Assert.IsType<Multipart>(smtp.Message.Body);
        var part = Assert.Single(multipart.OfType<MimePart>(), p => p.IsAttachment);
        Assert.Equal(ContentEncoding.Base64, part.ContentTransferEncoding);

        var options = FormatOptions.Default.Clone();
        options.NewLineFormat = NewLineFormat.Dos;
        using var serialized = new MemoryStream();
        smtp.Message.WriteTo(options, serialized);

        Assert.False(ContainsBareLineFeed(serialized.ToArray()));
    }

    [Fact]
    public void CreateMessage_ExplicitTransferEncoding_IsRespected() {
        var descriptor = new ByteArrayAttachmentDescriptor(Encoding.UTF8.GetBytes("hello world"), "greeting.txt") {
            ContentType = "text/plain",
            TransferEncoding = ContentEncoding.QuotedPrintable,
        };

        var smtp = new Smtp {
            From = "a@b.com",
            To = new object[] { "c@d.com" },
            Subject = "test",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { descriptor },
        };

        smtp.CreateMessage();

        var multipart = Assert.IsType<Multipart>(smtp.Message.Body);
        var part = Assert.Single(multipart.OfType<MimePart>(), p => p.IsAttachment);
        Assert.Equal(ContentEncoding.QuotedPrintable, part.ContentTransferEncoding);
    }

    private static bool ContainsBareLineFeed(byte[] bytes) {
        for (var index = 0; index < bytes.Length; index++) {
            if (bytes[index] == 0x0a && (index == 0 || bytes[index - 1] != 0x0d)) {
                return true;
            }
        }

        return false;
    }
}
