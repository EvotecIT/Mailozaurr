using System.Net.Http;
using System.Reflection;

namespace Mailozaurr.Tests;

public class SendGridClientDisposeTests {
    [Fact]
    public async Task Dispose_DisposesHttpClient() {
        var client = new SendGridClient();
        FieldInfo field = typeof(SendGridClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var httpClient = (HttpClient)field.GetValue(client)!;

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => httpClient.GetAsync("http://example.com"));
    }
}

