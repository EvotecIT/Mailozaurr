using Mailozaurr.MailboxExport;
using OfficeIMO.Email.Store;

namespace Mailozaurr.Tests;

public sealed partial class GraphMailboxPstExportTests {
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ResumeRecoversFinalizedPartAndVerifiesPublishedBytes(bool split, bool published, bool modified) {
        var options = NewOptions("publish-resume.pst", GraphMailboxExportItemTypes.Mail);
        if (split) options.Split = new EmailStorePstSplitOptions(maxEstimatedBytesPerPart: 40 * 1024);
        string output = split ? GraphMailboxPstExporter.GetPartPath(options.DestinationPath, 1) : options.DestinationPath;
        Directory.CreateDirectory(output);

        Exception? failure = await Record.ExceptionAsync(() => NewExporter(options).ExportAsync());

        Assert.True(failure is IOException or UnauthorizedAccessException, "An output directory must prevent the staged PST from being published.");
        GraphMailboxPstCheckpoint checkpoint = GraphMailboxPstCheckpoint.Read(options.CheckpointPath!);
        Assert.Equal(!split, checkpoint.IsFinalPart);
        string stagingDirectory = Path.Combine(_directory, ".mailozaurr-pst-" + checkpoint.StagingId.ToString("N"));
        string stage = Path.Combine(stagingDirectory, "part.pst");
        int stagedItems = split ? 2 : 7;
        Assert.Equal(stagedItems, ReadPart(stage).Count);
        string stagedHash = Sha256(stage);
        int previousMimeRequests = _graph.MimeRequests.Count;
        int previousListRequests = _graph.MessageListRequests.Count;
        Directory.Delete(output);
        options.Resume = true;
        if (published) File.Move(stage, output);
        if (modified) {
            using (FileStream file = new FileStream(output, FileMode.Append, FileAccess.Write)) file.WriteByte(0x7f);

            InvalidDataException mismatch = await Assert.ThrowsAsync<InvalidDataException>(() => NewExporter(options).ExportAsync());

            Assert.Contains("checkpoint SHA-256", mismatch.Message);
            Assert.Equal(previousMimeRequests, _graph.MimeRequests.Count);
            Assert.Equal(previousListRequests, _graph.MessageListRequests.Count);
            Assert.True(File.Exists(options.CheckpointPath));
            return;
        }

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.True(report.Resumed);
        Assert.True(report.IsSuccessful);
        Assert.Equal(7, report.Written.Messages);
        Assert.Equal(split ? new[] { 2, 2, 2, 1 } : new[] { 7 }, report.Parts.Select(part => part.ItemCount));
        Assert.Equal(stagedHash, Sha256(output));
        Assert.Equal(stagedHash, report.Parts[0].Sha256);
        string[] subjects = report.Parts.SelectMany(part => ReadPart(part.Path)).Select(item => item.Document.Subject!).ToArray();
        Assert.Equal(7, subjects.Length);
        Assert.Equal(7, subjects.Distinct().Count());
        Assert.All(report.Parts, part => {
            Assert.True(part.Verified);
            Assert.Equal(part.ItemCount, part.VerifiedItemCount);
            Assert.Equal(part.ItemCount, ReadPart(part.Path).Count);
            Assert.Equal(Sha256(part.Path), part.Sha256);
        });
        if (split) {
            Assert.DoesNotContain(_graph.MimeRequests.Skip(previousMimeRequests), id => id is "inbox-1" or "inbox-2");
        } else {
            Assert.Equal(previousMimeRequests, _graph.MimeRequests.Count);
            Assert.Equal(previousListRequests, _graph.MessageListRequests.Count);
        }
        Assert.False(File.Exists(options.CheckpointPath));
        Assert.Empty(Directory.GetDirectories(_directory, ".mailozaurr-pst-*"));
        using FileStream unlocked = File.Open(options.CheckpointPath + ".mailozaurr.lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task ResumeReplaysOnlyUncommittedPartWhenFinalizationLosesNativeJournal() {
        var options = NewOptions("lost-journal.pst", GraphMailboxExportItemTypes.Mail);
        options.Split = new EmailStorePstSplitOptions(maxEstimatedBytesPerPart: 40 * 1024);
        options.CheckpointIntervalItems = 2;
        // Part 1 is published; part 2 has two items and the next download would trigger its finalization.
        await InterruptExportAsync(options, "deleted-1");
        string completed = GraphMailboxPstExporter.GetPartPath(options.DestinationPath, 1);
        string completedHash = Sha256(completed);
        GraphMailboxPstCheckpoint checkpoint = GraphMailboxPstCheckpoint.Read(options.CheckpointPath!);
        Assert.Equal(2, checkpoint.ReadStart().PartNumber);
        string stagingDirectory = Path.Combine(_directory, ".mailozaurr-pst-" + checkpoint.StagingId.ToString("N"));
        GraphMailboxPstExportState active;
        using (EmailStorePstWriter writer = EmailStorePstWriter.Resume(Path.Combine(stagingDirectory, "writer.checkpoint"), out byte[]? state)) {
            active = GraphMailboxPstExportState.Deserialize(state);
        }
        Assert.Equal(2, active.PartItems);
        // Model the durable finalization intent before a native journal is lost, using its real captured application state.
        checkpoint.SaveFinalizing(options.CheckpointPath!, active, isFinalPart: false);
        string nodes = Assert.Single(Directory.GetFiles(stagingDirectory, "*.nodes"));
        File.Delete(nodes);
        int previousRequests = _graph.MimeRequests.Count;
        options.Resume = true;

        GraphMailboxPstExportReport report = await NewExporter(options).ExportAsync();

        Assert.True(report.Resumed);
        Assert.True(report.IsSuccessful);
        Assert.Equal(7, report.Written.Messages);
        Assert.Equal(new[] { 2, 2, 2, 1 }, report.Parts.Select(part => part.ItemCount));
        Assert.Equal(completedHash, Sha256(completed));
        Assert.DoesNotContain(_graph.MimeRequests.Skip(previousRequests), id => id is "inbox-1" or "inbox-2");
        string[] subjects = report.Parts.SelectMany(part => ReadPart(part.Path)).Select(item => item.Document.Subject!).ToArray();
        Assert.Equal(7, subjects.Length);
        Assert.Equal(7, subjects.Distinct().Count());
        Assert.All(report.Parts, part => {
            Assert.True(part.Verified);
            Assert.Equal(part.ItemCount, part.VerifiedItemCount);
            Assert.Equal(Sha256(part.Path), part.Sha256);
        });
        Assert.False(File.Exists(options.CheckpointPath));
        Assert.Empty(Directory.GetDirectories(_directory, ".mailozaurr-pst-*"));
    }
}
