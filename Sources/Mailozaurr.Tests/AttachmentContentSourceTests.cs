using Mailozaurr.Definitions;
using OfficeIMO.Email;

namespace Mailozaurr.Tests;

public sealed class AttachmentContentSourceTests {
    [Fact]
    public async Task ContentSourceDescriptor_OpensIndependentSyncAndAsyncStreams() {
        var source = new RecordingContentSource(new byte[] { 1, 2, 3 });
        var descriptor = new ContentSourceAttachmentDescriptor(source, "data.bin");

        using Stream first = descriptor.OpenContentStream();
        using Stream second = await descriptor.OpenContentStreamAsync();

        Assert.NotSame(first, second);
        Assert.Equal(3, descriptor.Length);
        Assert.Equal(2, source.OpenCount);
        Assert.Equal(new byte[] { 1, 2, 3 }, ReadAll(first));
        Assert.Equal(new byte[] { 1, 2, 3 }, ReadAll(second));
    }

    [Fact]
    public void FileDescriptor_StreamsFromTheFileInsteadOfMaterializingAByteArray() {
        string path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[] { 4, 5, 6 });
        try {
            var descriptor = new FileAttachmentDescriptor(path);

            using Stream stream = descriptor.OpenContentStream();

            Assert.IsType<FileStream>(stream);
            Assert.Equal(3, descriptor.Length);
            Assert.Equal(new byte[] { 4, 5, 6 }, ReadAll(stream));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OfficeIMOAdapter_ReusesReopenableAttachmentContent() {
        var source = new OfficeIMORecordingSource(new byte[] { 7, 8, 9 });
        var attachment = new EmailAttachment {
            FileName = "office.bin",
            ContentType = "application/octet-stream",
            ContentSource = source,
            Length = 3
        };

        AttachmentDescriptor descriptor = attachment.ToMailozaurrAttachment();
        using Stream first = descriptor.OpenContentStream();
        using Stream second = await descriptor.OpenContentStreamAsync();

        Assert.Equal(2, source.OpenCount);
        Assert.Equal(3, descriptor.Length);
        Assert.Equal(new byte[] { 7, 8, 9 }, ReadAll(first));
        Assert.Equal(new byte[] { 7, 8, 9 }, ReadAll(second));
    }

    private static byte[] ReadAll(Stream stream) {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class RecordingContentSource : IAttachmentContentSource {
        private readonly byte[] _content;
        internal RecordingContentSource(byte[] content) => _content = content;
        internal int OpenCount { get; private set; }
        public long? Length => _content.LongLength;
        public Stream OpenRead() {
            OpenCount++;
            return new MemoryStream(_content, writable: false);
        }
        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OpenRead());
        }
    }

    private sealed class OfficeIMORecordingSource : IEmailContentSource {
        private readonly byte[] _content;
        internal OfficeIMORecordingSource(byte[] content) => _content = content;
        internal int OpenCount { get; private set; }
        public long? Length => _content.LongLength;
        public Stream OpenRead() {
            OpenCount++;
            return new MemoryStream(_content, writable: false);
        }
        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OpenRead());
        }
    }
}
