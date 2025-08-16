using System.Reflection;
using MailKit.Net.Imap;
using MailKit.Search;
using Xunit;
using Mailozaurr;

namespace Mailozaurr.Tests;

public class ImapIdleListenerTests {
    [Fact]
    public void Constructor_SetsSearchQuery() {
        var client = new ImapClient();
        var query = SearchQuery.SubjectContains("Test");

        var listener = new ImapIdleListener(client, searchQuery: query);
        var field = typeof(ImapIdleListener).GetField("_searchQuery", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(listener);

        Assert.Equal(query, field);
    }
}
