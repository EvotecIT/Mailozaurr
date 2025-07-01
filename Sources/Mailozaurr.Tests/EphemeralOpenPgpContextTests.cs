using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class EphemeralOpenPgpContextTests
{
    [Fact]
    public async Task CreateTempDirectories_AreUniqueAcrossThreads()
    {
        const int count = 20;
        var bag = new ConcurrentBag<string>();
        var field = typeof(EphemeralOpenPgpContext).GetField("_tempDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(() =>
        {
            using var ctx = new EphemeralOpenPgpContext();
            var dir = (string)field.GetValue(ctx)!;
            bag.Add(dir);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(count, bag.Distinct(StringComparer.Ordinal).Count());
        foreach (var dir in bag)
        {
            Assert.False(Directory.Exists(dir));
        }
    }
}
