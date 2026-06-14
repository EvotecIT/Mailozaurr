using Mailozaurr;
using System;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphEventBuilderTests {
    [Fact]
    public void Builder_CreatesEvent() {
        GraphEvent ev = new GraphEventBuilder()
            .Subject("Test")
            .Start(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc))
            .End(new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc))
            .Attendee("user@example.com", "User");
        Assert.Equal("Test", ev.Subject);
        Assert.NotNull(ev.Start);
        Assert.NotNull(ev.End);
        Assert.NotNull(ev.Attendees);
        Assert.Contains(ev.Attendees!, a => a.EmailAddress.Email.Address == "user@example.com");
    }
}