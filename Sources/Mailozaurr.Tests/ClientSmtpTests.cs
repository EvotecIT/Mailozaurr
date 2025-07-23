using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using Xunit;
using Mailozaurr;

namespace Mailozaurr.Tests;

public class ClientSmtpTests
{
    [Fact]
    public void Ctor_DefaultsPriorityToNormal()
    {
        var client = new ClientSmtp();
        Assert.Equal(MessagePriority.Normal, client.Priority);
    }
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

    [Fact]
    public void ConvertStringToMailboxAddresses_InvalidInput_LogsWarning()
    {
        var client = new ClientSmtp();
        MethodInfo? method = typeof(ClientSmtp).GetMethod(
            "ConvertStringToMailboxAddresses",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var messages = new List<string>();
        void Handler(object? _, LogEventArgs e) => messages.Add(e.Message);
        LoggingMessages.Logger.OnWarningMessage += Handler;

        var enumerable = (IEnumerable<MailboxAddress>)method!.Invoke(client, new object[] { "invalid@" })!;
        var result = enumerable.ToList();

        LoggingMessages.Logger.OnWarningMessage -= Handler;
        Assert.Empty(result);
        Assert.Contains(messages, static m => m.Contains("invalid@"));
    }
}
