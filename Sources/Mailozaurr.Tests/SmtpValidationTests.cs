using Xunit;

namespace Mailozaurr.Tests;

public class SmtpValidationTests {
    [Fact]
    public void TryValidateServer_WithMissingServer_Fails() {
        var ok = SmtpValidation.TryValidateServer(null, 25, out var error);
        Assert.False(ok);
        Assert.Contains("server", error);
    }

    [Fact]
    public void TryValidateServer_WithInvalidPort_Fails() {
        var ok = SmtpValidation.TryValidateServer("smtp.example.com", 0, out var error);
        Assert.False(ok);
        Assert.Contains("port", error);
    }

    [Fact]
    public void TryValidateServer_WithValidInputs_Succeeds() {
        var ok = SmtpValidation.TryValidateServer("smtp.example.com", 25, out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateCredentials_WithMissingUsername_Fails() {
        var ok = SmtpValidation.TryValidateCredentials(null, "secret", out var error);
        Assert.False(ok);
        Assert.Contains("username", error);
    }

    [Fact]
    public void TryValidateCredentials_WithMissingPassword_Fails() {
        var ok = SmtpValidation.TryValidateCredentials("user", null, out var error);
        Assert.False(ok);
        Assert.Contains("password", error);
    }

    [Fact]
    public void TryValidateCredentials_WithValidInputs_Succeeds() {
        var ok = SmtpValidation.TryValidateCredentials("user", "secret", out var error);
        Assert.True(ok);
        Assert.Null(error);
    }
}

