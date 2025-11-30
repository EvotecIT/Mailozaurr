using System.Text.Json;
using Xunit;

namespace Mailozaurr.Tests;

public class SerializationContextTests {
    [Fact]
    public void ShouldProvideTypeInfoForCommonDictionaries() {
        Assert.NotNull(MailozaurrJsonContext.Default.DictionaryStringString);
        Assert.NotNull(MailozaurrJsonContext.Default.DictionaryStringBool);
        Assert.NotNull(MailozaurrJsonContext.Default.DictionaryStringInt);
    }

    [Fact]
    public void ShouldSerializeAndDeserializeGmailMessage() {
        var message = new GmailMessage { Id = "id", ThreadId = "tid", Snippet = "hi" };
        var json = JsonSerializer.Serialize(message, MailozaurrJsonContext.Default.GmailMessage);
        var roundtrip = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GmailMessage);
        Assert.NotNull(roundtrip);
        Assert.Equal(message.Id, roundtrip!.Id);
        Assert.Equal(message.ThreadId, roundtrip.ThreadId);
        Assert.Equal(message.Snippet, roundtrip.Snippet);
    }

    [Fact]
    public void ShouldSerializeAndDeserializePendingMessageRecord() {
        var record = new PendingMessageRecord {
            MessageId = "mid",
            MimeMessage = "body",
            Timestamp = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
            AttemptCount = 2,
            Provider = EmailProvider.Gmail,
            ProviderData = new() { ["k"] = "v" }
        };
        var json = JsonSerializer.Serialize(record, MailozaurrJsonContext.Default.PendingMessageRecord);
        var roundtrip = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.PendingMessageRecord);
        Assert.NotNull(roundtrip);
        Assert.Equal(record.MessageId, roundtrip!.MessageId);
        Assert.Equal(record.Provider, roundtrip.Provider);
        Assert.Equal(record.ProviderData["k"], roundtrip.ProviderData["k"]);
    }
}
