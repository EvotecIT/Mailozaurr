using System;
using System.Reflection;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public class ClientSmtpTests
{
    [Fact]
    public void ConvertToMailboxAddress_InvalidType_IncludesValueInException()
    {
        var client = new ClientSmtp();
        MethodInfo? method = typeof(ClientSmtp).GetMethod(
            "ConvertToMailboxAddress",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var enumerable = (IEnumerable<MailboxAddress>)method!.Invoke(client, new object[] { 42 })!;
        using var enumerator = enumerable.GetEnumerator();
        var ex = Assert.Throws<ArgumentException>(() => enumerator.MoveNext());
        Assert.Contains("42", ex.Message, StringComparison.Ordinal);
    }
}
