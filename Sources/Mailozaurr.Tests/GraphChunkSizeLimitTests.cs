using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphChunkSizeLimitTests {
    [Fact]
    public void ChunkSize_SetAboveLimit_IsCapped() {
        using Graph graph = new();
        graph.ChunkSize = Graph.MaxChunkSize * 2;
        Assert.Equal(Graph.MaxChunkSize, graph.ChunkSize);
    }

    [Fact]
    public async Task PrepareByteArrayContentForUpload_EnforcesMaxChunkSize() {
        string tmp = Path.GetTempFileName();
        int fileSize = Graph.MaxChunkSize + 1024;
        File.WriteAllBytes(tmp, new byte[fileSize]);
        using Graph graph = new();
        MethodInfo? method = typeof(Graph).GetMethod(
            "PrepareByteArrayContentForUpload",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        List<StreamContent> chunks = (List<StreamContent>)method!.Invoke(
            graph,
            new object[] { tmp, graph.ChunkSize * 2, default(System.Threading.CancellationToken) })!;
        File.Delete(tmp);
        Assert.Equal(2, chunks.Count);
        byte[] chunk0 = await chunks[0].ReadAsByteArrayAsync();
        byte[] chunk1 = await chunks[1].ReadAsByteArrayAsync();
        Assert.Equal(Graph.MaxChunkSize, chunk0.Length);
        Assert.Equal(fileSize - Graph.MaxChunkSize, chunk1.Length);
    }
}