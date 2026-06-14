using System.Collections.Generic;
using System.Reflection;

namespace Mailozaurr.Tests;

public class SendGridConvertToEmailObjectTests {
    [Fact]
    public void ConvertToEmailObject_String_ReturnsEmail() {
        var client = new SendGridClient();
        MethodInfo? method = typeof(SendGridClient).GetMethod("ConvertToEmailObject", BindingFlags.NonPublic | BindingFlags.Instance);
        var result = method?.Invoke(client, new object?[] { "user@example.com" }) as SendGridEmailAddress;
        Assert.NotNull(result);
        Assert.Equal("user@example.com", result!.Email);
        Assert.Null(result.Name);
    }

    [Fact]
    public void ConvertToEmailObject_Dictionary_ReturnsEmail() {
        var client = new SendGridClient();
        MethodInfo? method = typeof(SendGridClient).GetMethod("ConvertToEmailObject", BindingFlags.NonPublic | BindingFlags.Instance);
        var input = new Dictionary<string, object> {
            ["Email"] = "dict@example.com",
            ["Name"] = "Dict"
        };
        var result = method?.Invoke(client, new object?[] { input }) as SendGridEmailAddress;
        Assert.NotNull(result);
        Assert.Equal("dict@example.com", result!.Email);
        Assert.Equal("Dict", result.Name);
    }

    [Fact]
    public void ConvertToEmailObject_SendGridEmailAddress_ReturnsSameInstance() {
        var client = new SendGridClient();
        MethodInfo? method = typeof(SendGridClient).GetMethod("ConvertToEmailObject", BindingFlags.NonPublic | BindingFlags.Instance);
        var address = new SendGridEmailAddress { Email = "sg@example.com", Name = "SG" };
        var result = method?.Invoke(client, new object?[] { address }) as SendGridEmailAddress;
        Assert.Same(address, result);
    }
}