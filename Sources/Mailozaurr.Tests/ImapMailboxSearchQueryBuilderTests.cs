using MailKit.Search;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class ImapMailboxSearchQueryBuilderTests {
    [Fact]
    public void Tokenize_ReturnsEmpty_ForNullOrWhitespace() {
        Assert.Empty(ImapMailboxSearchQueryBuilder.Tokenize(null));
        Assert.Empty(ImapMailboxSearchQueryBuilder.Tokenize(string.Empty));
        Assert.Empty(ImapMailboxSearchQueryBuilder.Tokenize(" \t \r\n "));
    }

    [Fact]
    public void Tokenize_SplitsOnWhitespaceAndTrims() {
        var tokens = ImapMailboxSearchQueryBuilder.Tokenize(" alpha\tbeta \r\ngamma  ");

        Assert.Equal(3, tokens.Count);
        Assert.Equal("alpha", tokens[0]);
        Assert.Equal("beta", tokens[1]);
        Assert.Equal("gamma", tokens[2]);
    }

    [Fact]
    public void Build_ReturnsSearchQuery() {
        var query = ImapMailboxSearchQueryBuilder.Build(
            unseenOnly: true,
            subjectContains: "subject",
            fromContains: "sender",
            toContains: "recipient",
            bodyContains: "body",
            query: "token");

        Assert.NotNull(query);
        Assert.IsAssignableFrom<SearchQuery>(query);
    }
}
