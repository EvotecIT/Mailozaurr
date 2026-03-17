using System;
using System.Threading.Tasks;
using MailKit.Security;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpSessionServiceTests {
    [Fact]
    public async Task ConnectAndAuthenticateAsync_ReturnsSuccess() {
        var request = new SmtpSessionRequest {
            Server = "smtp.test",
            Port = 587,
            SecureSocketOptions = SecureSocketOptions.Auto,
            UserName = "user",
            Password = "pass",
            ConnectAsync = _ => Task.FromResult(new SmtpResult(true, EmailAction.Connect, string.Empty, string.Empty, "smtp.test", 587, TimeSpan.Zero)),
            AuthenticateAsync = _ => Task.FromResult(new SmtpResult(true, EmailAction.Authenticate, string.Empty, string.Empty, "smtp.test", 587, TimeSpan.Zero))
        };

        var smtp = new Smtp();
        var result = await SmtpSessionService.ConnectAndAuthenticateAsync(smtp, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(SecureSocketOptions.Auto, result.SecureSocketOptions);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_FlagsConnectFailures() {
        var request = new SmtpSessionRequest {
            Server = "smtp.test",
            Port = 587,
            SecureSocketOptions = SecureSocketOptions.Auto,
            UserName = "user",
            Password = "pass",
            ConnectAsync = _ => Task.FromResult(new SmtpResult(false, EmailAction.Connect, string.Empty, string.Empty, "smtp.test", 587, TimeSpan.Zero, error: "nope"))
        };

        var smtp = new Smtp();
        var result = await SmtpSessionService.ConnectAndAuthenticateAsync(smtp, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("connect_failed", result.ErrorCode);
        Assert.Equal("nope", result.Error);
        Assert.True(result.IsTransient);
    }

    [Fact]
    public async Task ConnectAndAuthenticateAsync_FlagsAuthFailures() {
        var request = new SmtpSessionRequest {
            Server = "smtp.test",
            Port = 587,
            SecureSocketOptions = SecureSocketOptions.Auto,
            UserName = "user",
            Password = "pass",
            ConnectAsync = _ => Task.FromResult(new SmtpResult(true, EmailAction.Connect, string.Empty, string.Empty, "smtp.test", 587, TimeSpan.Zero)),
            AuthenticateAsync = _ => Task.FromResult(new SmtpResult(false, EmailAction.Authenticate, string.Empty, string.Empty, "smtp.test", 587, TimeSpan.Zero, error: "bad auth"))
        };

        var smtp = new Smtp();
        var result = await SmtpSessionService.ConnectAndAuthenticateAsync(smtp, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("auth_failed", result.ErrorCode);
        Assert.Equal("bad auth", result.Error);
        Assert.False(result.IsTransient);
    }
}
