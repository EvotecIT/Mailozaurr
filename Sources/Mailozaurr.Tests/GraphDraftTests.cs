using System;
using System.IO;
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
}
