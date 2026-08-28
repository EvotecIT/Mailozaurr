using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class HtmlUtilsTests {
    [Fact]
    public void ExtractLocalImagePaths_ReplacesOnlySrcValues() {
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
    public void ExtractLocalImagePaths_PrecompiledRegex_MatchesInlineImplementation() {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var html = $"<IMG SRC=\"{tmp}\"><p>{tmp}</p>";

        var expectedPaths = new List<string>();
        const string pattern = @"(?<=<img[^>]+src=['""])([^'""]+)(?=['""])";
        var expectedHtml = Regex.Replace(html, pattern, match => {
            var path = match.Value;
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
                return path;
            }
            if (File.Exists(path)) {
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
    public async Task DownloadRemoteImagesAsync_ReplacesOnlySrcValuesAsync() {
        const string url = "https://example.com/img.png";
        var html = $"<img src=\"{url}\"><p>{url}</p>";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(html, TrustedTestOptions());

            Assert.Contains("cid:img.png", result);
            Assert.Contains($"<p>{url}</p>", result);
            var image = Assert.Single(images);
            Assert.Equal("image/png", image.MediaType);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_DoesNotCreateExtraHandlers() {
        const string url = "https://example.com/img.png";
        var html = $"<img src=\"{url}\">";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            await HtmlUtils.DownloadRemoteImagesAsync(html, TrustedTestOptions());
            await HtmlUtils.DownloadRemoteImagesAsync(html, TrustedTestOptions());

            Assert.Equal(2, handler.Requests.Count);
            Assert.Same(handler, handlerField!.GetValue(HtmlUtils.HttpClient));
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_DetectsMultipleImagesRegardlessOfCaseAsync() {
        const string url1 = "https://example.com/img.png";
        const string url2 = "HTTPS://example.com/photo.jpg";
        var html = $"<IMG SRC=\"{url1}\"><img SRC=\"{url2}\"><p>{url1}</p>";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(new byte[] { 1 }) {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
                }
            },
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(new byte[] { 2 }) {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") }
                }
            });
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(html, TrustedTestOptions());

            Assert.Contains("cid:img.png", result);
            Assert.Contains("cid:photo.jpg", result);
            Assert.Contains($"<p>{url1}</p>", result);
            Assert.Equal(2, images.Count);
            Assert.Contains(images, i => i.MediaType == "image/png");
            Assert.Contains(images, i => i.MediaType == "image/jpeg");
            Assert.Equal(2, handler.Requests.Count);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_PropagatesCancellationAsync() {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await HtmlUtils.DownloadRemoteImagesAsync("<img src=\"https://example.com/img.png\">", cts.Token));
    }

    [Theory]
    [InlineData("https://127.0.0.1/image.png")]
    [InlineData("https://10.0.0.1/image.png")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://[::1]/image.png")]
    [InlineData("https://[fd00::1]/image.png")]
    public async Task DownloadRemoteImagesAsync_RejectsNonPublicDestinations(string url) {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent(new byte[] { 1 }) {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });
        await WithHandlerAsync(handler, async () => {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync($"<img src=\"{url}\">");

            Assert.Contains(url, result, StringComparison.Ordinal);
            Assert.Empty(images);
            Assert.Empty(handler.Requests);
        });
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_ValidatesEveryRedirectDestination() {
        const string source = "https://93.184.216.34/image.png";
        var redirect = new HttpResponseMessage(HttpStatusCode.Redirect) {
            Headers = { Location = new Uri("https://127.0.0.1/private.png") }
        };
        var handler = new RecordingHandler(redirect);
        await WithHandlerAsync(handler, async () => {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync($"<img src=\"{source}\">");

            Assert.Contains(source, result, StringComparison.Ordinal);
            Assert.Empty(images);
            Assert.Single(handler.Requests);
        });
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_RejectsOversizedAndActiveContent() {
        const string oversized = "https://example.com/large.png";
        const string svg = "https://example.com/active.svg";
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 }) {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
                }
            },
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("<svg><script/></svg>")) {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/svg+xml") }
                }
            });
        var options = TrustedTestOptions();
        options.MaxImageBytes = 3;
        await WithHandlerAsync(handler, async () => {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(
                $"<img src=\"{oversized}\"><img src=\"{svg}\">",
                options);

            Assert.Contains(oversized, result, StringComparison.Ordinal);
            Assert.Contains(svg, result, StringComparison.Ordinal);
            Assert.Empty(images);
        });
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_AllocatesUniqueContentIdsForDuplicateFileNames() {
        const string first = "https://one.example/image.png";
        const string second = "https://two.example/image.png";
        var handler = new RecordingHandler(
            ImageResponse(new byte[] { 1 }),
            ImageResponse(new byte[] { 2 }));
        await WithHandlerAsync(handler, async () => {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(
                $"<img src=\"{first}\"><img src=\"{second}\">",
                TrustedTestOptions());

            Assert.Equal(2, images.Count);
            Assert.Equal(2, images.Select(image => image.ContentId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains("cid:image.png", result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task DownloadRemoteImagesAsync_EnforcesTheAggregateBudgetAcrossImages() {
        const string first = "https://one.example/first.png";
        const string second = "https://two.example/second.png";
        var handler = new RecordingHandler(
            ImageResponse(new byte[] { 1, 2 }),
            ImageResponse(new byte[] { 3, 4 }));
        var options = TrustedTestOptions();
        options.MaxTotalBytes = 3;
        await WithHandlerAsync(handler, async () => {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(
                $"<img src=\"{first}\"><img src=\"{second}\">",
                options);

            var image = Assert.Single(images);
            Assert.Equal(new byte[] { 1, 2 }, image.Data);
            Assert.Contains("cid:first.png", result, StringComparison.Ordinal);
            Assert.Contains(second, result, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.2.3.4", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("100.100.0.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("198.51.100.1", false)]
    [InlineData("203.0.113.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    public void Remote_address_policy_distinguishes_public_destinations(string value, bool expected) =>
        Assert.Equal(expected, RemoteImageDownloader.IsPublicAddress(IPAddress.Parse(value)));

    private static RemoteImageDownloadOptions TrustedTestOptions() => new() {
        AllowPrivateNetworkAddresses = true
    };

    private static HttpResponseMessage ImageResponse(byte[] content) => new(HttpStatusCode.OK) {
        Content = new ByteArrayContent(content) {
            Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
        }
    };

    private static async Task WithHandlerAsync(RecordingHandler handler, Func<Task> action) {
        var client = HtmlUtils.HttpClient;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (HttpMessageHandler)handlerField!.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            await action();
        } finally {
            handlerField.SetValue(client, original);
        }
    }
}
