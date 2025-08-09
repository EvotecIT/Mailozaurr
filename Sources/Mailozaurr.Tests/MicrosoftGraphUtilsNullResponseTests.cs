using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class MicrosoftGraphUtilsNullResponseTests {
    private sealed class NullContentHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth2")) {
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") });
        }
    }

    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    [Fact]
    public async Task NewRuleAsync_NullResponse_Throws() {
        var handler = new NullContentHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
            var rule = new GraphInboxRule();
            await Assert.ThrowsAsync<System.IO.InvalidDataException>(() =>
                MicrosoftGraphUtils.NewRuleAsync(cred, "user@example.com", rule));
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task NewEventAsync_NullResponse_Throws() {
        var handler = new NullContentHandler();
        var field = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)field.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };
            var ev = new GraphEvent();
            await Assert.ThrowsAsync<System.IO.InvalidDataException>(() =>
                MicrosoftGraphUtils.NewEventAsync(cred, "user@example.com", ev));
        } finally {
            handlerField.SetValue(client, original);
        }
    }
}
