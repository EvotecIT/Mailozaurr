using System;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class ValidatorTests
{
    [Fact]
    public void ValidateEmail_DisposableDomain_IsMarkedDisposable()
    {
        var result = Validator.ValidateEmail("user@example.com");
        Assert.True(result.IsValid);
        Assert.True(result.IsDisposable);
    }

    [Fact]
    public void ValidateEmail_AllowedDomain_NotDisposable()
    {
        var result = Validator.ValidateEmail("user@allowed.example.com");
        Assert.True(result.IsValid);
        Assert.False(result.IsDisposable);
    }

    [Fact]
    public void ValidateEmail_InvalidEmail_ReturnsFalse()
    {
        var result = Validator.ValidateEmail("invalid");
        Assert.False(result.IsValid);
    }
}
