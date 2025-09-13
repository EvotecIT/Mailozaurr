using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class IsDisposableEmailTests
{
    private static readonly MethodInfo Method = typeof(Validator).GetMethod("IsDisposableEmail", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void IsDisposableEmail_ValidEmail_ReturnsTrue()
    {
        var result = (bool)Method.Invoke(null, new object[] { "user@example.com" })!;
        Assert.True(result);
    }

    [Fact]
    public void IsDisposableEmail_MissingAt_ReturnsFalse()
    {
        var result = (bool)Method.Invoke(null, new object[] { "invalidemail.com" })!;
        Assert.False(result);
    }

    [Fact]
    public void IsDisposableEmail_MultipleAt_ReturnsFalse()
    {
        var result = (bool)Method.Invoke(null, new object[] { "user@@example.com" })!;
        Assert.False(result);
    }
}

