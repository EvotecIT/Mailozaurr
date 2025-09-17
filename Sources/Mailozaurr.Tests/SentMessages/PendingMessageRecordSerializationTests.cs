using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Mailozaurr.Tests;

public sealed class PendingMessageRecordSerializationTests {
    public static IEnumerable<object[]> ProviderRecords() {
        yield return new object[] {
            EmailProvider.None,
            new Dictionary<string, string> {
                ["Server"] = "smtp.example.com",
                ["Port"] = "587",
                ["Secure"] = "true"
            }
        };
        yield return new object[] {
            EmailProvider.SendGrid,
            new Dictionary<string, string> {
                ["ApiKeyId"] = "sendgrid-key",
                ["TemplateId"] = "templ-001"
            }
        };
        yield return new object[] {
            EmailProvider.Gmail,
            new Dictionary<string, string> {
                ["RefreshToken"] = "refresh-token",
                ["ClientId"] = "gmail-client"
            }
        };
        yield return new object[] {
            EmailProvider.Mailgun,
            new Dictionary<string, string> {
                ["Domain"] = "mg.example.com",
                ["ApiBase"] = "https://api.mailgun.net"
            }
        };
        yield return new object[] {
            EmailProvider.SES,
            new Dictionary<string, string> {
                ["Region"] = "us-east-1",
                ["ConfigurationSet"] = "config-set"
            }
        };
    }

    [Theory]
    [MemberData(nameof(ProviderRecords))]
    public void SerializationRoundTripPreservesProviderData(EmailProvider provider, Dictionary<string, string> providerData) {
        var record = new PendingMessageRecord {
            MessageId = $"message-{provider}",
            Timestamp = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            NextAttemptAt = DateTimeOffset.Parse("2024-01-01T01:00:00Z"),
            AttemptCount = 3,
            MimeMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes($"Message body for {provider}")),
            Provider = provider
        };
        foreach (var pair in providerData) {
            record.ProviderData[pair.Key] = pair.Value;
        }

        var json = JsonSerializer.Serialize(record);
        var restored = JsonSerializer.Deserialize<PendingMessageRecord>(json);

        Assert.NotNull(restored);
        Assert.Equal(provider, restored!.Provider);
        Assert.Equal(record.ProviderData, restored.ProviderData);
        Assert.Equal(record.MessageId, restored.MessageId);
        Assert.Equal(record.AttemptCount, restored.AttemptCount);
    }

    [Fact]
    public void DeserializeLegacyRecordWithoutProviderInformationInitializesDefaults() {
        const string legacyJson = "{\"MessageId\":\"legacy\",\"Timestamp\":\"2024-01-10T00:00:00+00:00\",\"NextAttemptAt\":\"2024-01-10T01:00:00+00:00\",\"MimeMessage\":\"bWVzc2FnZQ==\",\"Server\":\"smtp.old.example.com\",\"Port\":25,\"UserName\":\"legacy-user\",\"Password\":null}";

        var record = JsonSerializer.Deserialize<PendingMessageRecord>(legacyJson);

        Assert.NotNull(record);
        Assert.Equal(EmailProvider.None, record!.Provider);
        Assert.Empty(record.ProviderData);
        Assert.Equal(0, record.AttemptCount);
    }

    [Fact]
    public void DeserializeNullProviderDataInitializesDictionary() {
        const string json = "{\"MessageId\":\"null-provider-data\",\"Timestamp\":\"2024-02-01T00:00:00+00:00\",\"NextAttemptAt\":\"2024-02-01T00:10:00+00:00\",\"MimeMessage\":\"bWVzc2FnZQ==\",\"Provider\":3,\"ProviderData\":null}";

        var record = JsonSerializer.Deserialize<PendingMessageRecord>(json);

        Assert.NotNull(record);
        Assert.Equal(EmailProvider.SES, record!.Provider);
        Assert.Empty(record.ProviderData);
        Assert.Equal(0, record.AttemptCount);
    }
}
