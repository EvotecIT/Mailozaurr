using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Management.Automation;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphExceptionStackTraceTests {
    [Fact]
    public async Task SendMessageAsync_Failure_PreservesStackTrace() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest) {
            Content = new StringContent("{\"error\":{\"code\":\"BadRequest\",\"message\":\"Invalid\",\"innerError\":{\"requestId\":\"1\",\"date\":\"2020-01-01\"}}}")
        });

        using var graph = new Graph {
            AccessToken = "token",
            TokenType = "Bearer",
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            ErrorAction = ActionPreference.Stop
        };
        var field = typeof(Graph).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(graph, new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<GraphApiException>(() => graph.SendMessageAsync());
        Assert.Contains(nameof(Graph.SendMessageAsync), ex.StackTrace);
    }

    [Fact]
    public async Task SendDraftMessage_Failure_PreservesStackTrace() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest) {
            Content = new StringContent("{\"error\":{\"code\":\"BadRequest\",\"message\":\"Invalid\",\"innerError\":{\"requestId\":\"1\",\"date\":\"2020-01-01\"}}}")
        });

        using var graph = new Graph {
            AccessToken = "token",
            TokenType = "Bearer",
            From = "from@example.com",
            To = new object[] { "to@example.com" },
            Subject = "sub",
            HTML = "body",
            ContentType = "HTML",
            MessageContainer = new GraphMessageContainer {
                Message = new GraphMessage {
                    From = new GraphEmailAddress { Email = new GraphEmail { Address = "from@example.com" } }
                }
            }
        };
        var field = typeof(Graph).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(graph, new HttpClient(handler));

        var draft = new GraphMessage { Id = "id" };
        var ex = await Assert.ThrowsAsync<GraphApiException>(() => graph.SendDraftMessage(draft));
        Assert.Contains(nameof(Graph.SendDraftMessage), ex.StackTrace);
    }
}