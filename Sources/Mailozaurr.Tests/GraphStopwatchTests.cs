using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphStopwatchTests {
    private static void SetHttpClient(Graph graph, HttpMessageHandler handler) {
        var field = typeof(Graph).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(graph, new HttpClient(handler));
    }

    private static HttpResponseMessage CreateJsonResponse(string content) {
        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(content)
        };
    }

    [Fact]
    public async Task ConnectO365GraphAsync_SuccessiveCallsHaveIndependentDurations() {
        var interCallDelay = TimeSpan.FromSeconds(1);
        var maxExpectedDuration = TimeSpan.FromMilliseconds(800);
        var responsePayload = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
        var handler = new RecordingHandler(
            CreateJsonResponse(responsePayload),
            CreateJsonResponse(responsePayload));

        using var graph = new Graph();
        SetHttpClient(graph, handler);
        graph.Authenticate(new NetworkCredential("client@tenant", "secret"));

        await Task.Delay(interCallDelay);
        var first = await graph.ConnectO365GraphAsync();
        Assert.True(first.Status);
        Assert.True(
            first.TimeToExecute < maxExpectedDuration,
            $"Expected first elapsed time to be well below the idle delay of {interCallDelay.TotalMilliseconds}ms but was {first.TimeToExecute.TotalMilliseconds}ms");

        await Task.Delay(interCallDelay);
        var second = await graph.ConnectO365GraphAsync();
        Assert.True(second.Status);
        Assert.True(
            second.TimeToExecute < maxExpectedDuration,
            $"Expected second elapsed time to be well below the idle delay of {interCallDelay.TotalMilliseconds}ms but was {second.TimeToExecute.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task SendMessageAsync_SuccessiveCallsHaveIndependentDurations() {
        var interCallDelay = TimeSpan.FromSeconds(1);
        var maxExpectedDuration = TimeSpan.FromMilliseconds(800);
        var handler = new RecordingHandler(
            CreateJsonResponse("{}"),
            CreateJsonResponse("{}"));

        using var graph = new Graph();
        SetHttpClient(graph, handler);
        graph.From = "sender@example.com";
        graph.To = new object[] { "recipient@example.com" };
        graph.Subject = "Test";
        graph.HTML = "<p>Hello</p>";
        graph.ContentType = "HTML";
        graph.AccessToken = "token";
        graph.TokenType = "Bearer";

        await Task.Delay(interCallDelay);
        var first = await graph.SendMessageAsync();
        Assert.True(first.Status);
        Assert.True(
            first.TimeToExecute < maxExpectedDuration,
            $"Expected first elapsed time to be well below the idle delay of {interCallDelay.TotalMilliseconds}ms but was {first.TimeToExecute.TotalMilliseconds}ms");

        await Task.Delay(interCallDelay);
        var second = await graph.SendMessageAsync();
        Assert.True(second.Status);
        Assert.True(
            second.TimeToExecute < maxExpectedDuration,
            $"Expected second elapsed time to be well below the idle delay of {interCallDelay.TotalMilliseconds}ms but was {second.TimeToExecute.TotalMilliseconds}ms");
    }
}