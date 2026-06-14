using Xunit;

namespace Mailozaurr.Tests;

public sealed class ProtocolAuthTests {
    [Theory]
    [InlineData(null, ProtocolAuthMode.Basic)]
    [InlineData("", ProtocolAuthMode.Basic)]
    [InlineData(" ", ProtocolAuthMode.Basic)]
    [InlineData("basic", ProtocolAuthMode.Basic)]
    [InlineData("BASIC", ProtocolAuthMode.Basic)]
    [InlineData("oauth2", ProtocolAuthMode.OAuth2)]
    [InlineData("oauth", ProtocolAuthMode.OAuth2)]
    [InlineData("xoauth2", ProtocolAuthMode.OAuth2)]
    [InlineData("unexpected", ProtocolAuthMode.Basic)]
    public void ParseMode_ParsesExpectedValues(string? raw, ProtocolAuthMode expected) {
        var mode = ProtocolAuth.ParseMode(raw);
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void ParseMode_UsesProvidedFallbackForUnknownValue() {
        var mode = ProtocolAuth.ParseMode("unexpected", fallback: ProtocolAuthMode.OAuth2);
        Assert.Equal(ProtocolAuthMode.OAuth2, mode);
    }
}