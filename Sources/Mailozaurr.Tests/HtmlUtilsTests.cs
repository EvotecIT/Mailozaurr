using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
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
    public void ExtractLocalImagePaths_UsesHtmlDomForFlexibleAttributeSyntax() {
        var tmp = Path.GetTempFileName();
        File.WriteAllText(tmp, "data");
        var html = $"<p data-src=\"{tmp}\">before</p><IMG alt=\"src='{tmp}'\" SRC = \"{tmp}\"><p>{tmp}</p>";

        var (actualHtml, actualPaths) = HtmlUtils.ExtractLocalImagePaths(html);

        Assert.Contains($"data-src=\"{tmp}\"", actualHtml, StringComparison.Ordinal);
        Assert.Contains($"alt=\"src='{tmp}'\"", actualHtml, StringComparison.Ordinal);
        Assert.Contains($"src=\"cid:{Path.GetFileName(tmp)}\"", actualHtml, StringComparison.Ordinal);
        Assert.Contains($">{tmp}</p>", actualHtml, StringComparison.Ordinal);
        Assert.Equal(new[] { tmp }, actualPaths);

        File.Delete(tmp);
    }

    [Fact]
    public void ExtractLocalImages_AllocatesUniqueContentIdsForDuplicateFileNames() {
        string root = Path.Combine(Path.GetTempPath(), "MailozaurrHtml-" + Guid.NewGuid().ToString("N"));
        string firstDirectory = Path.Combine(root, "one");
        string secondDirectory = Path.Combine(root, "two");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        string first = Path.Combine(firstDirectory, "logo.png");
        string second = Path.Combine(secondDirectory, "logo.png");
        File.WriteAllBytes(first, new byte[] { 1 });
        File.WriteAllBytes(second, new byte[] { 2 });
        try {
            var (rendered, images) = HtmlUtils.ExtractLocalImages(
                $"<img src='{first}'><img src='{second}'>");

            Assert.Equal(2, images.Count);
            Assert.Equal(2, images.Select(image => image.ContentId)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains("src=\"cid:" + images[0].ContentId + "\"", rendered, StringComparison.Ordinal);
            Assert.Contains("src=\"cid:" + images[1].ContentId + "\"", rendered, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExtractLocalImages_ReusesContentIdForRepeatedCanonicalPath() {
        string root = Path.Combine(Path.GetTempPath(), "MailozaurrHtml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "logo.png");
        File.WriteAllBytes(file, new byte[] { 1 });
        try {
            string equivalentPath = Path.Combine(root, ".", "logo.png");

            var (rendered, images) = HtmlUtils.ExtractLocalImages(
                $"<img src='{file}'><img src='{equivalentPath}'>");

            HtmlUtils.LocalImage image = Assert.Single(images);
            Assert.Equal(
                2,
                rendered.Split(new[] { "cid:" + image.ContentId }, StringSplitOptions.None).Length - 1);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExtractLocalImages_AvoidsContentIdsAlreadyUsedByTheMessage() {
        string file = Path.Combine(Path.GetTempPath(), "logo.png");
        File.WriteAllBytes(file, new byte[] { 1 });
        try {
            var (rendered, images) = HtmlUtils.ExtractLocalImages(
                $"<img src='{file}'>",
                new[] { "logo.png" });

            HtmlUtils.LocalImage image = Assert.Single(images);
            Assert.False(string.Equals("logo.png", image.ContentId, StringComparison.OrdinalIgnoreCase));
            Assert.Contains("cid:" + image.ContentId, rendered, StringComparison.Ordinal);
        } finally {
            File.Delete(file);
        }
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
            var options = new RemoteImageDownloadOptions { AllowUnpinnedDnsResolution = true };
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(
                $"<img src=\"{source}\">",
                options);

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
    public async Task DownloadRemoteImagesAsync_KeepsCaseDistinctPathsDistinctAndAvoidsReservedIds() {
        const string first = "https://example.com/Logo.png";
        const string second = "https://example.com/logo.png";
        var handler = new RecordingHandler(
            ImageResponse(new byte[] { 1 }),
            ImageResponse(new byte[] { 2 }));
        await WithHandlerAsync(handler, async () => {
            var (result, images) = await HtmlUtils.DownloadRemoteImagesAsync(
                $"<img src=\"{first}\"><img src=\"{second}\">",
                TrustedTestOptions(),
                new[] { "Logo.png" });

            Assert.Equal(2, images.Count);
            Assert.Equal(2, handler.Requests.Count);
            Assert.All(images, image =>
                Assert.False(string.Equals("Logo.png", image.ContentId, StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(2, images.Select(image => image.ContentId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.DoesNotContain(first, result, StringComparison.Ordinal);
            Assert.DoesNotContain(second, result, StringComparison.Ordinal);
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

    [Fact]
    public void SecureRemoteImageTransport_IsPinnedOnSupportedRuntimesAndFailsClosedElsewhere() {
        var options = new RemoteImageDownloadOptions();
        using HttpClient? client = RemoteImageDownloader.CreatePinnedClient(options);
#if NET8_0_OR_GREATER
        Assert.NotNull(client);
        var handler = (HttpMessageHandler)GetHandlerField().GetValue(client!)!;
        var socketsHandler = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.NotNull(socketsHandler.ConnectCallback);
        Assert.False(socketsHandler.UseProxy);
#else
        Assert.Null(client);
#endif
    }

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

    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("HttpClient handler field not found.");
}
