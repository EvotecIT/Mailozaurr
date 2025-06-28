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
        using Graph graph = new Graph();
        MethodInfo? method = typeof(Graph).GetMethod(
            "PrepareByteArrayContentForUpload",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        Task<List<StreamContent>> task = (Task<List<StreamContent>>)method!.Invoke(graph, new object[] { tmp, 10, default(System.Threading.CancellationToken) })!;
        List<StreamContent> chunks = await task;
        File.Delete(tmp);
        Assert.Equal(3, chunks.Count);
        Assert.Equal("bytes 0-9/25", chunks[0].Headers.GetValues("Content-Range").First());
        Assert.Equal("bytes 10-19/25", chunks[1].Headers.GetValues("Content-Range").First());
        Assert.Equal("bytes 20-24/25", chunks[2].Headers.GetValues("Content-Range").First());
    }

    [Fact]
    public async Task PrepareByteArrayContentForUpload_CreatesIndependentBuffers()
    {
        string tmp = Path.GetTempFileName();
        byte[] allBytes = Enumerable.Range(0, 25).Select(b => (byte)b).ToArray();
        File.WriteAllBytes(tmp, allBytes);
        using Graph graph = new Graph();
        MethodInfo? method = typeof(Graph).GetMethod(
            "PrepareByteArrayContentForUpload",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        Task<List<StreamContent>> task = (Task<List<StreamContent>>)method!.Invoke(
            graph,
            new object[] { tmp, 10, default(System.Threading.CancellationToken) })!;
        List<StreamContent> chunks = await task;
        File.Delete(tmp);
        byte[] chunk0 = await chunks[0].ReadAsByteArrayAsync();
        byte[] chunk1 = await chunks[1].ReadAsByteArrayAsync();
        byte[] chunk2 = await chunks[2].ReadAsByteArrayAsync();
        Assert.Equal(allBytes.Take(10), chunk0);
        Assert.Equal(allBytes.Skip(10).Take(10), chunk1);
        Assert.Equal(allBytes.Skip(20).Take(5), chunk2);
    }
}
