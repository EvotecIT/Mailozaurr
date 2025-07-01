using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    [Fact]
    public void ConvertFromOAuth2Credential_ThrowsArgumentNullException_WhenCredentialIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Mailozaurr.Helpers.ConvertFromOAuth2Credential(null!));
    }

    [Fact]
    public void UniqueAddresses_RemovesDuplicates_IgnoringCaseAndWhitespace()
    {
        var addresses = new object[]
        {
            " Test@example.com ",
            "test@example.com",
            "other@example.com",
            "Other@example.com "
        };
        var seen = new HashSet<string>();
        var result = Mailozaurr.Helpers
            .UniqueAddresses(addresses, seen)
            .Select(Mailozaurr.Helpers.GetEmailAddress)
            .ToArray();

        Assert.Equal(new[] { " Test@example.com ", "other@example.com" }, result);
    }

    [Fact]
    public async Task PostWebhookAsync_CancellationRequested_ThrowsAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = new SmtpResult(true, EmailAction.Send, string.Empty, string.Empty, string.Empty, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            Mailozaurr.Helpers.PostWebhookAsync("http://localhost", result, cts.Token));
    }
}
