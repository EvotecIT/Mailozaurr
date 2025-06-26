using System;
using System.IO;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphCreateMessageTests
{
    [Fact]
    public void CreateMessage_WithValidData_BuildsJson()
    {
        using var graph = new Graph
        {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = "body",
            ContentType = "HTML"
        };
        graph.CreateMessage();
        Assert.Contains("subject", graph.MessageJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to@example.com", graph.MessageJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateAttachments_WithMissingFile_ThrowsFileNotFoundException()
    {
        using var graph = new Graph
        {
            Attachments = new object[] { "missing.file" }
        };
        Assert.Throws<FileNotFoundException>(() => graph.CreateAttachments());
    }

    [Fact]
    public void CreateMessage_WithLargeAttachment_DoesNotIncludeAttachment()
    {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, new byte[4000001]);
        using var graph = new Graph
        {
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "subject",
            HTML = "body",
            ContentType = "HTML",
            Attachments = new object[] { tmp }
        };
        graph.CreateAttachments();
        graph.CreateMessage();
        File.Delete(tmp);
        Assert.True(graph.IsLargerAttachment);
        Assert.Null(graph.MessageContainer.Message.Attachments);
    }
}
