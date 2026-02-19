using Xunit;

namespace Mailozaurr.Tests;

public class ImapSentMessageOperationsTests {
    [Theory]
    [InlineData("<id@example.test>", "id@example.test")]
    [InlineData("  <id@example.test>  ", "id@example.test")]
    [InlineData("id@example.test", "id@example.test")]
    [InlineData("<id@example.test", "id@example.test")]
    [InlineData("id@example.test>", "id@example.test")]
    public void NormalizeMessageIdToken_NormalizesBracketsAndWhitespace(string input, string expected) {
        var normalized = ImapSentMessageOperations.NormalizeMessageIdToken(input);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<>")]
    public void NormalizeMessageIdToken_ReturnsNull_ForEmptyInput(string? input) {
        var normalized = ImapSentMessageOperations.NormalizeMessageIdToken(input);

        Assert.Null(normalized);
    }
}
