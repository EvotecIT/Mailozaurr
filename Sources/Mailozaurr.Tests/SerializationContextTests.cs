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

    [Fact]
    public void ShouldDeserializeGraphAuthorizationExpiresOnFromUnixSeconds() {
        const long unixSeconds = 1700000000;
        var json = $"{{\"token_type\":\"Bearer\",\"access_token\":\"abc\",\"expires_on\":\"{unixSeconds}\"}}";
        var auth = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GraphAuthorization);
        Assert.NotNull(auth);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), auth!.ExpiresOn);
    }

    [Fact]
    public void ShouldDeserializeGraphAuthorizationExpiresOnFromUnixSecondsNumber() {
        const long unixSeconds = 1700000000;
        var json = $"{{\"token_type\":\"Bearer\",\"access_token\":\"abc\",\"expires_on\":{unixSeconds}}}";
        var auth = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GraphAuthorization);
        Assert.NotNull(auth);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), auth!.ExpiresOn);
    }

    [Fact]
    public void ShouldDeserializeGraphAuthorizationExpiresOnFromUnixMilliseconds() {
        const long unixMilliseconds = 1700000000000;
        var json = $"{{\"token_type\":\"Bearer\",\"access_token\":\"abc\",\"expires_on\":{unixMilliseconds}}}";
        var auth = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GraphAuthorization);
        Assert.NotNull(auth);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds), auth!.ExpiresOn);
    }

    [Fact]
    public void ShouldDeserializeGraphAuthorizationExpiresOnFromIsoString() {
        const string iso = "2023-11-14T22:13:20.000Z";
        var json = $"{{\"token_type\":\"Bearer\",\"access_token\":\"abc\",\"expires_on\":\"{iso}\"}}";
        var auth = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GraphAuthorization);
        Assert.NotNull(auth);
        Assert.Equal(DateTimeOffset.Parse(iso), auth!.ExpiresOn);
    }

    [Fact]
    public void ShouldHandleGraphAuthorizationExpiresOnNullOrEmpty() {
        var jsonNull = "{\"token_type\":\"Bearer\",\"access_token\":\"abc\",\"expires_on\":null}";
        var authNull = JsonSerializer.Deserialize(jsonNull, MailozaurrJsonContext.Default.GraphAuthorization);
        Assert.NotNull(authNull);
        Assert.Equal(DateTimeOffset.MinValue, authNull!.ExpiresOn);

        var jsonEmpty = "{\"token_type\":\"Bearer\",\"access_token\":\"abc\",\"expires_on\":\"\"}";
        var authEmpty = JsonSerializer.Deserialize(jsonEmpty, MailozaurrJsonContext.Default.GraphAuthorization);
        Assert.NotNull(authEmpty);
        Assert.Equal(DateTimeOffset.MinValue, authEmpty!.ExpiresOn);
    }

    [Fact]
    public void ShouldHandleGraphAuthorizationExpiresOnInvalid() {
        var json = "{\"token_type\":\"Bearer\",\"access_token\":\"abc\",\"expires_on\":\"not-a-date\"}";
        var auth = JsonSerializer.Deserialize(json, MailozaurrJsonContext.Default.GraphAuthorization);
        Assert.NotNull(auth);
        Assert.Equal(DateTimeOffset.MinValue, auth!.ExpiresOn);
    }
}
