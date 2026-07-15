#if NET8_0_OR_GREATER
using OfficeIMO.Email;

namespace Mailozaurr.Tests;

public sealed class MailFilePerformanceEvidenceTests {
    private const int AttachmentByteCount = 3 * 1024 * 1024;

    [Fact]
    public void LargeEmlReadWithoutAttachmentsOrSignatureUsesTheBoundedOwnerPath() {
        string directory = CreateTempDirectory();
        try {
            string path = WriteLargeAttachmentEml(directory);
            var boundedOptions = new MailFileReaderOptions {
                IncludeAttachments = false,
                IncludeAttachmentContent = false,
                VerifySignature = false
            };
            var retainedOptions = new MailFileReaderOptions {
                IncludeAttachments = true,
                IncludeAttachmentContent = true,
                VerifySignature = false
            };

            // Warm both OfficeIMO read modes before comparing their steady-state allocations.
            _ = MailFileReader.Read(path, boundedOptions);
            _ = MailFileReader.Read(path, retainedOptions);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long beforeBounded = GC.GetAllocatedBytesForCurrentThread();
            MailFileMessage bounded = MailFileReader.Read(path, boundedOptions);
            long boundedAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeBounded;

            long beforeRetained = GC.GetAllocatedBytesForCurrentThread();
            MailFileMessage retained = MailFileReader.Read(path, retainedOptions);
            long retainedAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeRetained;

            long sourceBytes = new FileInfo(path).Length;
            EmailAttachment ownerAttachment = Assert.Single(bounded.OfficeDocument.Attachments);
            Assert.Empty(bounded.Attachments);
            Assert.Null(ownerAttachment.Content);
            Assert.Equal(AttachmentByteCount, ownerAttachment.Length);
            Assert.Null(bounded.OfficeDocument.RawSource);
            Assert.Null(bounded.SignatureIsValid);
            Assert.Equal(AttachmentByteCount, Assert.Single(retained.Attachments).Content!.Length);

            Assert.True(boundedAllocated <= (sourceBytes * 4L) + (8L * 1024L * 1024L),
                $"Bounded read allocated {boundedAllocated:N0} bytes for a {sourceBytes:N0}-byte EML.");
            Assert.True(retainedAllocated >= boundedAllocated + AttachmentByteCount,
                $"Retained-content read allocated {retainedAllocated:N0} bytes versus " +
                $"{boundedAllocated:N0} bytes for the bounded read.");

            Console.WriteLine(
                "EML bytes: {0:N0}; bounded allocations: {1:N0}; retained allocations: {2:N0}",
                sourceBytes, boundedAllocated, retainedAllocated);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTempDirectory() {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteLargeAttachmentEml(string directory) {
        string path = Path.Combine(directory, "large-bounded.eml");
        string attachment = Convert.ToBase64String(new byte[AttachmentByteCount],
            Base64FormattingOptions.InsertLineBreaks);
        File.WriteAllText(path, string.Join("\r\n", new[] {
            "From: sender@example.test",
            "To: recipient@example.test",
            "Subject: Large bounded attachment",
            "MIME-Version: 1.0",
            "Content-Type: multipart/mixed; boundary=mailozaurr-boundary",
            string.Empty,
            "--mailozaurr-boundary",
            "Content-Type: text/plain; charset=utf-8",
            string.Empty,
            "Small body",
            "--mailozaurr-boundary",
            "Content-Type: application/octet-stream; name=payload.bin",
            "Content-Disposition: attachment; filename=payload.bin",
            "Content-Transfer-Encoding: base64",
            string.Empty,
            attachment,
            "--mailozaurr-boundary--",
            string.Empty
        }));
        return path;
    }
}
#endif
