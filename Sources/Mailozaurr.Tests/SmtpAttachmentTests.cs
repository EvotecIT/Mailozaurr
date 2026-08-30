using Mailozaurr.Definitions;
using MimeKit;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAttachmentTests {
    [Fact]
    public void StreamAttachmentDescriptor_PreservesLegacyThreeParameterConstructor() {
        ConstructorInfo? constructor = typeof(StreamAttachmentDescriptor).GetConstructor(
            new[] { typeof(Stream), typeof(string), typeof(bool) });

        Assert.NotNull(constructor);
    }

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
    public void SmtpDispose_PreservesStreamAttachmentStagingWhenNoSendWasAttempted() {
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

            Assert.Single(Directory.GetFiles(directory));
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, descriptor.GetContentBytes());
            descriptor.Dispose();
            Assert.Empty(Directory.GetFiles(directory));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SmtpDryRun_PreservesStreamAttachmentStaging() {
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

                Assert.Single(Directory.GetFiles(directory));
                Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, descriptor.GetContentBytes());
            } finally {
                smtp.Dispose();
                descriptor.Dispose();
            }
            Assert.Empty(Directory.GetFiles(directory));
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
            descriptor.GetContentBytes();
            AttachmentDescriptorLifetime.MarkSendAttempted(new[] { descriptor });
            AttachmentDescriptorLifetime.ReleaseStaging(new[] { descriptor });

            Assert.Single(Directory.GetFiles(directory));
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, descriptor.GetContentBytes());
        } finally {
            descriptor.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AttachmentDescriptorLifetime_ReleasesStagingAfterTransportAttempt() {
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
            descriptor.GetContentBytes();

            AttachmentDescriptorLifetime.MarkSendAttempted(new[] { descriptor });
            AttachmentDescriptorLifetime.ReleaseStaging(new[] { descriptor });

            Assert.Empty(Directory.GetFiles(directory));
            Assert.Throws<ObjectDisposedException>(() => descriptor.OpenContentStream());
        } finally {
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

    [Fact]
    public void StreamAttachmentDescriptor_PoisonsNonSeekableSourceAfterStagingFailure() {
        using var descriptor = new StreamAttachmentDescriptor(
            new NonSeekableReadStream(new byte[] { 1, 2, 3, 4, 5 }),
            "too-large.bin",
            leaveStreamOpen: false,
            stagingOptions: new AttachmentStreamStagingOptions {
                MemoryThresholdBytes = 2,
                MaxBytes = 4
            });

        Assert.Throws<InvalidDataException>(() => descriptor.OpenContentStream());
        var retry = Assert.Throws<InvalidOperationException>(() => descriptor.OpenContentStream());
        Assert.IsType<InvalidDataException>(retry.InnerException);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void StreamAttachmentDescriptor_UsesPerUserDefaultStagingDirectoryOnUnix() {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows)) return;

        using var source = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var descriptor = new StreamAttachmentDescriptor(
            source,
            "staged.bin",
            stagingOptions: new AttachmentStreamStagingOptions {
                MemoryThresholdBytes = 2,
                MaxBytes = 10
            });
        string stagedPath;
        try {
            descriptor.GetContentBytes();
            var stagedPathField = typeof(StreamAttachmentDescriptor).GetField(
                "_stagedFilePath",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            stagedPath = Assert.IsType<string>(stagedPathField.GetValue(descriptor));

            Assert.True(File.Exists(stagedPath));
            Assert.Equal("attachments", Path.GetFileName(Path.GetDirectoryName(stagedPath)));
            Assert.Equal(
                "Mailozaurr-" + geteuid().ToString(System.Globalization.CultureInfo.InvariantCulture),
                Directory.GetParent(Path.GetDirectoryName(stagedPath)!)!.Name);
        } finally {
            descriptor.Dispose();
        }

        Assert.False(File.Exists(stagedPath));
    }

    [Fact]
    public void StreamAttachmentDescriptor_PreservesExistingCallerDirectoryPermissions() {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows)) return;

        string directory = Path.Combine(Path.GetTempPath(), "MailozaurrStage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        const UnixFileMode originalMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute;
        File.SetUnixFileMode(directory, originalMode);
        try {
            using var descriptor = new StreamAttachmentDescriptor(
                new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }),
                "staged.bin",
                stagingOptions: new AttachmentStreamStagingOptions {
                    MemoryThresholdBytes = 2,
                    MaxBytes = 10,
                    TempDirectory = directory
                });

            descriptor.GetContentBytes();

            Assert.Equal(originalMode, File.GetUnixFileMode(directory));
            string stagedPath = Assert.Single(Directory.GetFiles(directory));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(stagedPath));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [System.Runtime.InteropServices.DllImport("libc")]
    private static extern uint geteuid();
#endif

    private sealed class NonSeekableReadStream : Stream {
        private readonly MemoryStream _inner;

        internal NonSeekableReadStream(byte[] content) =>
            _inner = new MemoryStream(content, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
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
