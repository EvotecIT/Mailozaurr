using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr;
using Xunit;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public class GraphMailboxPermissionTests {
    private sealed class RecordingHandler : HttpMessageHandler {
        public TaskCompletionSource<object?> GraphRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool GraphRequestCancelled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth2")) {
                var json = "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            }

            GraphRequestStarted.TrySetResult(null);
            try {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                GraphRequestCancelled = true;
                throw;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    private static FieldInfo GetHandlerField() =>
        typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("HttpClient handler field not found");

    [Fact]
    public void Constructor_ParsesRoles_IgnoringCase() {
        var raw = new Dictionary<string, object> {
            ["roles"] = new object[] { "owner", "read", "write" }
        };
        var perm = new Mailozaurr.GraphMailboxPermission(raw);
        Assert.NotNull(perm.Roles);
        Assert.Contains(GraphMailboxRole.Owner, perm.Roles!);
        Assert.Contains(GraphMailboxRole.Read, perm.Roles!);
        Assert.Contains(GraphMailboxRole.Write, perm.Roles!);
    }

    [Fact]
    public async Task RemoveMailboxPermissionAsync_UsesProvidedCancellationToken() {
        var handler = new RecordingHandler();
        var clientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)clientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var cred = new GraphCredential { ClientId = Guid.NewGuid().ToString("N"), DirectoryId = "tenant", ClientSecret = "secret" };
            using var cts = new CancellationTokenSource();
            var removalTask = MicrosoftGraphUtils.RemoveMailboxPermissionAsync(cred, "user@example.com", "perm", cts.Token);
            var started = await Task.WhenAny(handler.GraphRequestStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(handler.GraphRequestStarted.Task, started);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await removalTask);
            Assert.True(handler.GraphRequestCancelled);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task GetMailboxPermissionsAsync_UsesProvidedCancellationToken() {
        var handler = new RecordingHandler();
        var clientField = typeof(MicrosoftGraphUtils).GetField("HttpClient", BindingFlags.NonPublic | BindingFlags.Static)!;
        var client = (HttpClient)clientField.GetValue(null)!;
        var handlerField = GetHandlerField();
        var original = (HttpMessageHandler)handlerField.GetValue(client)!;
        handlerField.SetValue(client, handler);
        try {
            var cred = new GraphCredential { ClientId = Guid.NewGuid().ToString("N"), DirectoryId = "tenant", ClientSecret = "secret" };
            using var cts = new CancellationTokenSource();
            var permissionsTask = MicrosoftGraphUtils.GetMailboxPermissionsAsync(cred, "user@example.com", cts.Token);
            var started = await Task.WhenAny(handler.GraphRequestStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(handler.GraphRequestStarted.Task, started);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await permissionsTask);
            Assert.True(handler.GraphRequestCancelled);
        } finally {
            handlerField.SetValue(client, original);
        }
    }

    [Fact]
    public async Task ClearJunkMailAsync_Cancelled_ThrowsOperationCanceledException() {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cred = new GraphCredential { ClientId = "id", DirectoryId = "tenant", ClientSecret = "secret" };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            MicrosoftGraphUtils.ClearJunkMailAsync(
                cred,
                "user@example.com",
                cancellationToken: cts.Token));
    }
}
