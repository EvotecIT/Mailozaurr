using System.Linq;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class DownloadRemoteImagesTests
{
    [Fact]
    public async Task DownloadRemoteImagesAsync_LimitsConcurrency()
    {
        var handler = new CountingHandler(TimeSpan.FromMilliseconds(100));
        var client = new HttpClient(handler);
        var property = typeof(HtmlUtils).GetProperty("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var original = (HttpClient)property.GetValue(null)!;
        property.SetValue(null, client);
        try
        {
            var html = string.Join("", Enumerable.Range(0, 5).Select(i => $"<img src=\"https://example.com/{i}.png\">"));
            var (_, images) = await HtmlUtils.DownloadRemoteImagesAsync(html, 2);
            Assert.Equal(5, images.Count);
            Assert.True(handler.MaxConcurrency <= 2);
            Assert.True(handler.MaxConcurrency > 1);
        }
        finally
        {
            property.SetValue(null, original);
        }
    }
}
