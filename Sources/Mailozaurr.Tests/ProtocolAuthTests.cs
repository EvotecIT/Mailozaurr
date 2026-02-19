using Xunit;

namespace Mailozaurr.Tests;

public class ProtocolAuthTests {
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("basic")]
    [InlineData("unknown")]
    public void ParseMode_ReturnsBasic_ForDefaultOrUnknownValues(string? value) {
        var mode = ProtocolAuth.ParseMode(value);

        Assert.Equal(ProtocolAuthMode.Basic, mode);
    }

    [Theory]
    [InlineData("oauth2")]
    [InlineData("xoauth2")]
    [InlineData("oauth")]
    [InlineData(" OAUTH2 ")]
    public void ParseMode_ReturnsOAuth2_ForAliases(string value) {
        var mode = ProtocolAuth.ParseMode(value);

        Assert.Equal(ProtocolAuthMode.OAuth2, mode);
    }
}
