using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class MailgunClientTests
{
    private class DummyCredentials : ICredentials
    {
        public NetworkCredential GetCredential(Uri uri, string authType) => new NetworkCredential();
    }

    [Fact]
    public void EmailDomain_InvalidAddress_ThrowsArgumentException()
    {
        using var client = new MailgunClient { From = "invalid" };
        PropertyInfo? prop = typeof(MailgunClient).GetProperty("EmailDomain", BindingFlags.NonPublic | BindingFlags.Instance);
        var ex = Assert.Throws<TargetInvocationException>(() => prop!.GetValue(client));
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("invalid", ex.InnerException!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateContentAsync_WithHeaders_IncludesHeaders()
    {
        using var client = new MailgunClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Headers = new Dictionary<string, string> { ["X-Test"] = "123" }
        };
        MethodInfo? method = typeof(MailgunClient).GetMethod("CreateContentAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task<MultipartFormDataContent>)method!.Invoke(client, new object[] { default(System.Threading.CancellationToken) })!;
        using var content = await task;
        string body = await content.ReadAsStringAsync();
        Assert.Contains("h:X-Test", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("123", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendEmailAsync_InvalidCredentials_ThrowsInvalidOperationException()
    {
        using var client = new MailgunClient
        {
            From = "sender@example.com",
            To = new List<object> { "to@example.com" },
            Credentials = new DummyCredentials()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendEmailAsync());
    }
}
