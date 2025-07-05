using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphBatchAndRetryTests
{
    private class BatchHandler : HttpMessageHandler
    {
        public HttpRequestMessage? BatchRequest;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth2"))
            {
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });
            }
            if (request.RequestUri!.AbsoluteUri.Contains("$batch"))
            {
                BatchRequest = request;
                var json = "{\"responses\":[{\"id\":\"1\",\"status\":202}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task SendMessageBatchAsync_BuildsBatchPayload()
    {
        var handler = new BatchHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try
        {
            using var graph = new Graph
            {
                From = "sender@example.com",
                To = new object[] { "recipient@example.com" },
                Subject = "sub",
                HTML = "body",
                ContentType = "HTML"
            };
            graph.Authenticate(new System.Net.NetworkCredential("id@tenant", "secret"));
            var result = await graph.SendMessageBatchAsync();
            Assert.True(result.Status);

            string payload = await handler.BatchRequest!.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            var req = doc.RootElement.GetProperty("requests")[0];
            Assert.Equal("1", req.GetProperty("id").GetString());
            Assert.Equal("POST", req.GetProperty("method").GetString());
            Assert.Equal($"users/{graph.MessageContainer.Message.From.Email.Address}/sendMail", req.GetProperty("url").GetString());
            Assert.Equal("application/json", req.GetProperty("headers").GetProperty("Content-Type").GetString());
            Assert.Equal("sub", req.GetProperty("body").GetProperty("message").GetProperty("subject").GetString());
        }
        finally
        {
            handlerField.SetValue(client, original);
        }
    }

    private class RetryHandler : HttpMessageHandler
    {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount < 3)
            {
                throw new HttpRequestException("fail");
            }
            var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    [Fact]
    public async Task ConnectO365GraphWithRetryAsync_RetriesUntilSuccess()
    {
        var handler = new RetryHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        var cacheField = typeof(MicrosoftGraphUtils).GetField("TokenCache", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, GraphAuthorization>)cacheField.GetValue(null)!;
        cache.Clear();
        string cachePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Mailozaurr", "oauth_cache.json");
        if (System.IO.File.Exists(cachePath)) System.IO.File.Delete(cachePath);
        try
        {
            var credential = new GraphCredential { ClientId = "id", ClientSecret = "secret", DirectoryId = "tenant" };
            string token = await MicrosoftGraphUtils.ConnectO365GraphWithRetryAsync(credential, "tenant", 2, 0, 1);
            Assert.Equal("Bearer token", token);
            Assert.Equal(3, handler.CallCount);
        }
        finally
        {
            handlerField.SetValue(client, original);
        }
    }
}
