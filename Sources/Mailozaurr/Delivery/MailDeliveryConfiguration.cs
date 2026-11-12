using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Application-owned profiles, credentials and delivery policies, constructible in code or JSON.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MailDeliveryConfiguration {
    /// <summary>Named Mailozaurr profiles and their external credential references.</summary>
    public Dictionary<string, MailDeliveryProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Named delivery policies selected by the application.</summary>
    public Dictionary<string, MailDeliveryRoute> Routes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the same types accepted by the typed engine.</summary>
    public static MailDeliveryConfiguration ParseJson(string json) =>
        JsonSerializer.Deserialize(json, MailDeliveryJsonContext.Default.MailDeliveryConfiguration)
        ?? throw new JsonException("Mail delivery configuration must be an object.");

    /// <summary>Reads an application's delivery configuration file.</summary>
    public static MailDeliveryConfiguration LoadJson(string path) => ParseJson(File.ReadAllText(path));
}

/// <summary>Uses the canonical Mailozaurr profile model for any supported send provider.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MailDeliveryProfile {
    /// <summary>Provider, sender, mailbox, transport settings and optional multi-server SMTP policy.</summary>
    public MailProfile Profile { get; set; } = new();

    /// <summary>Credential references keyed by MailSecretNames, resolved before every attempt.</summary>
    public Dictionary<string, MailCredentialReference> Credentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Profile selection and retry policy applied consistently across providers.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MailDeliveryRoute {
    /// <summary>Ordered profile IDs. A known rejection advances to the next eligible profile.</summary>
    public List<string> Profiles { get; set; } = new();

    /// <summary>Starting-profile selection across messages.</summary>
    public MailDeliveryStrategy Strategy { get; set; } = MailDeliveryStrategy.Failover;

    /// <summary>Additional passes over transiently failing profiles. Accepted and uncertain attempts never retry.</summary>
    public int RetryCount { get; set; }

    /// <summary>Initial delay between retry passes.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Multiplier applied to successive retry delays.</summary>
    public double RetryBackoff { get; set; } = 2;

    /// <summary>Upper bound on the retry delay.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Chooses the starting provider while preserving safe failover.</summary>
public enum MailDeliveryStrategy {
    /// <summary>Starts with the first configured profile.</summary>
    Failover,
    /// <summary>Cycles the starting profile for each message.</summary>
    RoundRobin,
    /// <summary>Chooses a random starting profile for each message.</summary>
    Random
}

/// <summary>Identifies a credential in an external source without carrying its value.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MailCredentialReference(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("key")] string Key);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(MailDeliveryConfiguration))]
internal partial class MailDeliveryJsonContext : JsonSerializerContext;
