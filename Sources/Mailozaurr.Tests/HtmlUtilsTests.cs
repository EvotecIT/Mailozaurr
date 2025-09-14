using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
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
    public void ExtractLocalImagePaths_PrecompiledRegex_MatchesInlineImplementation()
    {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var html = $"<IMG SRC=\"{tmp}\"><p>{tmp}</p>";

        var expectedPaths = new List<string>();
        const string pattern = @"(?<=<img[^>]+src=['""])([^'""]+)(?=['""])";
        var expectedHtml = Regex.Replace(html, pattern, match =>
        {
            var path = match.Value;
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            if (File.Exists(path))
            {
                var fileName = Path.GetFileName(path);
                expectedPaths.Add(path);
                return $"cid:{fileName}";
            }
            return path;
        }, RegexOptions.IgnoreCase);

        var (actualHtml, actualPaths) = HtmlUtils.ExtractLocalImagePaths(html);

        Assert.Equal(expectedHtml, actualHtml);
        Assert.Equal(expectedPaths, actualPaths);

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
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
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
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_DoesNotCreateExtraHandlers()
    {
        const string url = "https://example.com/img.png";
        var html = $"<img src=\"{url}\">";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try
        {
            await HtmlUtils.DownloadRemoteImagesAsync(html);
            await HtmlUtils.DownloadRemoteImagesAsync(html);

            Assert.Equal(2, handler.Requests.Count);
            Assert.Same(handler, handlerField!.GetValue(HtmlUtils.HttpClient));
        }
        finally
        {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_DetectsMultipleImagesRegardlessOfCaseAsync()
    {
        const string url1 = "https://example.com/img.png";
        const string url2 = "HTTPS://example.com/photo.jpg";
        var html = $"<IMG SRC=\"{url1}\"><img SRC=\"{url2}\"><p>{url1}</p>";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1 })
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
                }
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 2 })
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") }
                }
            });
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try
        {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(html);

            Assert.Contains("cid:img.png", result);
            Assert.Contains("cid:photo.jpg", result);
            Assert.Contains($"<p>{url1}</p>", result);
            Assert.Equal(2, images.Count);
            Assert.Contains(images, i => i.MediaType == "image/png");
            Assert.Contains(images, i => i.MediaType == "image/jpeg");
            Assert.Equal(2, handler.Requests.Count);
        }
        finally
        {
            handlerField.SetValue(client, original);
        }
    }
}

