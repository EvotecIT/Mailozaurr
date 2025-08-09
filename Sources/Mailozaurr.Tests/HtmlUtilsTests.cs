using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class HtmlUtilsTests
{
    [Fact]
    public void ExtractLocalImagePaths_ReplacesOnlySrcValues()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var html = $"<img src=\"{tmp}\"><p>{tmp}</p>";

        var (result, paths) = HtmlUtils.ExtractLocalImagePaths(html);

        Assert.Contains($"cid:{Path.GetFileName(tmp)}", result);
        Assert.Contains($"<p>{tmp}</p>", result);
        var path = Assert.Single(paths);
        Assert.Equal(tmp, path);

        File.Delete(tmp);
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_ReplacesOnlySrcValuesAsync()
    {
        const string url = "https://example.com/img.png";
        var html = $"<img src=\"{url}\"><p>{url}</p>";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });
        var property = typeof(HtmlUtils).GetProperty("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var original = (HttpClient)property.GetValue(null)!;
        property.SetValue(null, new HttpClient(handler));
        try
        {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(html);

            Assert.Contains("cid:img.png", result);
            Assert.Contains($"<p>{url}</p>", result);
            var image = Assert.Single(images);
            Assert.Equal("image/png", image.MediaType);
        }
        finally
        {
            property.SetValue(null, original);
        }
    }
}

