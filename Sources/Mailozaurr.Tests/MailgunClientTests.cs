using System;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class MailgunClientTests
{
    [Fact]
    public void EmailDomain_InvalidAddress_ThrowsArgumentException()
    {
        var client = new MailgunClient { From = "invalid" };
        PropertyInfo? prop = typeof(MailgunClient).GetProperty("EmailDomain", BindingFlags.NonPublic | BindingFlags.Instance);
        var ex = Assert.Throws<TargetInvocationException>(() => prop!.GetValue(client));
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("invalid", ex.InnerException!.Message, StringComparison.Ordinal);
    }
}
