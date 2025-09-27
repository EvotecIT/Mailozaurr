using System;
using System.Net;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphAuthenticateTests {
    private sealed class FakeCredentials : ICredentials {
        public NetworkCredential? GetCredential(Uri? uri, string? authType)
            => throw new NotSupportedException();
    }

    [Fact]
    public void Authenticate_WithNonNetworkCredential_ThrowsArgumentException() {
        using var graph = new Graph();
        var credentials = new FakeCredentials();

        var exception = Assert.Throws<ArgumentException>(() => graph.Authenticate(credentials));

        Assert.Contains("NetworkCredential", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("user@")]
    [InlineData("@tenant")]
    public void Authenticate_WithMalformedUserName_ThrowsArgumentException(string userName) {
        using var graph = new Graph();
        var credentials = new NetworkCredential(userName, "secret");

        var exception = Assert.Throws<ArgumentException>(() => graph.Authenticate(credentials));

        Assert.Contains("clientid@directoryid", exception.Message);
    }
}
