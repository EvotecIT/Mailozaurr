using System;
using MailKit.Search;
using Mailozaurr;
using Xunit;

namespace Mailozaurr.Tests;

public class QueryLanguageParserTests {
    [Fact]
    public void ParseQuery_ParsesBasicFields() {
        var result = MailboxSearcher.ParseQuery("from:boss subject:\"report\"");
        Assert.Equal("boss", result.FromContains);
        Assert.Equal("report", result.Subject);
    }

    [Fact]
    public void ParseQuery_ParsesDatesAndFlags() {
        var result = MailboxSearcher.ParseQuery("since:2024-01-01 before:2024-02-01 has:attachment priority:high");
        Assert.Equal(new DateTime(2024, 1, 1), result.Since);
        Assert.Equal(new DateTime(2024, 2, 1), result.Before);
        Assert.True(result.HasAttachment);
        Assert.Equal(MessagePriority.High, result.Priority);
    }
}
