using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphUploadRangeTests
{
    [Fact]
    public async Task PrepareByteArrayContentForUpload_ComputesRangesCorrectly()
    {
        string tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, Enumerable.Range(0, 25).Select(b => (byte)b).ToArray());
        Graph graph = new Graph();
        MethodInfo? method = typeof(Graph).GetMethod(
            "PrepareByteArrayContentForUpload",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        Task<List<ByteArrayContent>> task = (Task<List<ByteArrayContent>>)method!.Invoke(graph, new object[] { tmp, 10, default(System.Threading.CancellationToken) })!;
        List<ByteArrayContent> chunks = await task;
        File.Delete(tmp);
        Assert.Equal(3, chunks.Count);
        Assert.Equal("bytes 0-9/25", chunks[0].Headers.GetValues("Content-Range").First());
        Assert.Equal("bytes 10-19/25", chunks[1].Headers.GetValues("Content-Range").First());
        Assert.Equal("bytes 20-24/25", chunks[2].Headers.GetValues("Content-Range").First());
    }
}
