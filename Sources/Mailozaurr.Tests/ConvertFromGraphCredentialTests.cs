using Mailozaurr;
using System;
using Xunit;

namespace Mailozaurr.Tests;

public class ConvertFromGraphCredentialTests {
    [Fact]
    public void ThrowsOnNullUsername() {
        Assert.Throws<ArgumentNullException>(() => MicrosoftGraphUtils.ConvertFromGraphCredential(null!, "secret"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ThrowsOnWhitespaceUsername(string username) {
        Assert.Throws<ArgumentException>(() => MicrosoftGraphUtils.ConvertFromGraphCredential(username, "secret"));
    }

    [Fact]
    public void ThrowsOnNullPassword() {
        Assert.Throws<ArgumentNullException>(() => MicrosoftGraphUtils.ConvertFromGraphCredential("client@tenant", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ThrowsOnWhitespacePassword(string password) {
        Assert.Throws<ArgumentException>(() => MicrosoftGraphUtils.ConvertFromGraphCredential("client@tenant", password));
    }
}