using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphDraftTests
{
    [Fact]
    public void CreateDraft_LargeAttachments_ExcludesAttachments()
    {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[4_100_000]);
        using var graph = new Graph
        {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { tmp }
        };

        string json = graph.CreateDraft();
        File.Delete(tmp);
        Assert.True(graph.IsLargerAttachment);
        Assert.DoesNotContain("\"attachments\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("saveToSentItems", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareAttachments_LargeFiles_CreatePlaceholders()
    {
        string tmp1 = Path.GetTempFileName();
        string tmp2 = Path.GetTempFileName();
        File.WriteAllBytes(tmp1, new byte[3_000_000]);
        File.WriteAllBytes(tmp2, new byte[2_000_000]);
        using var graph = new Graph { Attachments = new object[] { tmp1, tmp2 } };
        graph.CreateAttachments();
        await graph.PrepareAttachments();
        File.Delete(tmp1);
        File.Delete(tmp2);
        Assert.True(graph.IsLargerAttachment);
        Assert.Equal(2, graph.AttachmentsPlaceHolders.Count);
        Assert.All(graph.AttachmentsPlaceHolders, p => Assert.False(string.IsNullOrWhiteSpace(p.FileName)));
    }

    [Fact]
    public async Task CreateGraphAttachment_MissingFile_ThrowsAndLogsWarning()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        using var graph = new Graph();

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => graph.CreateGraphAttachment(missing));
        Assert.Contains("Attachment file not found", exception.Message);
        Assert.Equal(missing, exception.FileName);

        var warnings = graph.LogCollector.Logs.ToArray();
        Assert.Contains(warnings, entry => entry.Type == LogType.Warning && entry.Message.IndexOf(missing, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public async Task PrepareAttachments_MissingFile_SkipsPlaceholderAndLogs()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        using var graph = new Graph { Attachments = new object[] { missing } };

        await graph.PrepareAttachments();

        Assert.Empty(graph.AttachmentsPlaceHolders);
        var warnings = graph.LogCollector.Logs.ToArray();
        Assert.True(warnings.Count(entry => entry.Type == LogType.Warning && entry.Message.IndexOf(missing, StringComparison.OrdinalIgnoreCase) >= 0) >= 1);
    }

    [Fact]
    public void CreateDraft_SetsImportanceFromPriority()
    {
        using var graph = new Graph
        {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            Priority = MessagePriority.High
        };

        string json = graph.CreateDraft();
        Assert.Contains("\"importance\":\"high\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDraftForMg_SetsImportanceFromPriority()
    {
        using var graph = new Graph
        {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            Priority = MessagePriority.Low
        };

        string json = graph.CreateDraftForMg();
        Assert.Contains("\"importance\":\"low\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
