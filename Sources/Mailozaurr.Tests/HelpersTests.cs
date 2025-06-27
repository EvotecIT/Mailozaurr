using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests;

public class HelpersTests
{
    [Fact]
    public void GetEmailAddress_ReturnsInputString_WhenStringProvided()
    {
        var result = Mailozaurr.Helpers.GetEmailAddress("test@example.com");
        Assert.Equal("test@example.com", result);
    }

    [Fact]
    public void GetEmailAddress_ReturnsEmailFromDictionary_WhenEmailKeyPresent()
    {
        var dict = new Dictionary<string, object> { { "Email", "dict@example.com" } };
        var result = Mailozaurr.Helpers.GetEmailAddress(dict);
        Assert.Equal("dict@example.com", result);
    }

    [Fact]
    public void GetEmailAddress_ReturnsEmpty_WhenEmailKeyMissing()
    {
        var dict = new Dictionary<string, object> { { "Name", "John" } };
        var result = Mailozaurr.Helpers.GetEmailAddress(dict);
        Assert.Equal(string.Empty, result);
    }

    private class CustomObject
    {
        public override string ToString() => "Custom";
    }

    [Fact]
    public void GetEmailAddress_ReturnsObjectToString_WhenNotStringOrDictionary()
    {
        var obj = new CustomObject();
        var result = Mailozaurr.Helpers.GetEmailAddress(obj);
        Assert.Equal("Custom", result);
    }
}
