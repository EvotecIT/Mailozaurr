using System;
using System.Net;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphAuthenticateTests
{
    [Fact]
    public void Authenticate_WithNonNetworkCredential_Throws()
    {
        using var graph = new Graph();
        ICredentials credentials = new DummyCredentials();

        var exception = Assert.Throws<ArgumentException>(() => graph.Authenticate(credentials));
        Assert.Contains("NetworkCredential", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("client")]
    [InlineData("client@")]
    [InlineData("@tenant")]
    public void Authenticate_WithMalformedUserName_Throws(string username)
    {
        using var graph = new Graph();
        var credential = new NetworkCredential(username, "secret");

        var exception = Assert.Throws<ArgumentException>(() => graph.Authenticate(credential));
        Assert.Contains("clientid@directoryid", exception.Message);
    }

    private sealed class DummyCredentials : ICredentials
    {
        public NetworkCredential? GetCredential(Uri uri, string authType) => null;
    }
}
