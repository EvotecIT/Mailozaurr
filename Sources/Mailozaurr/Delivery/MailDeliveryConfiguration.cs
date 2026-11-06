using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Shareable delivery profiles and ordered routes. Credential values live outside this model.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MailDeliveryConfiguration {
    /// <summary>Named transport profiles, reusable across routes.</summary>
    public Dictionary<string, MailDeliveryProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Named routes whose profile IDs are tried in order.</summary>
    public Dictionary<string, List<string>> Routes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the same options used to construct a delivery engine in code.</summary>
    public static MailDeliveryConfiguration ParseJson(string json) =>
        JsonSerializer.Deserialize(json, MailDeliveryJsonContext.Default.MailDeliveryConfiguration)
        ?? throw new JsonException("Mail delivery configuration must be an object.");

    /// <summary>Reads a mail delivery configuration file.</summary>
    public static MailDeliveryConfiguration LoadJson(string path) => ParseJson(File.ReadAllText(path));
}

/// <summary>A sender identity and one configured transport.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MailDeliveryProfile {
    /// <summary>Mailbox address used as the sender. For Graph this is also the sending mailbox.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Microsoft Graph application settings.</summary>
    public GraphDeliveryOptions? Graph { get; set; }

    /// <summary>SMTP relay settings.</summary>
    public SmtpProfileDeliveryOptions? Smtp { get; set; }
}

/// <summary>Graph application authentication settings for a sending mailbox.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GraphDeliveryOptions {
    /// <summary>Entra tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Entra application identifier.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Reference to the application's current client secret.</summary>
    public MailCredentialReference? ClientSecret { get; set; }
}

/// <summary>SMTP relay and optional username/password authentication.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SmtpProfileDeliveryOptions {
    /// <summary>Relay hostname or IP address.</summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>Relay TCP port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Connection security. Password authentication requires TLS.</summary>
    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

    /// <summary>Username for authenticated SMTP. Omit both username and password for a trusted relay.</summary>
    public string? UserName { get; set; }

    /// <summary>Reference to the current SMTP password.</summary>
    public MailCredentialReference? Password { get; set; }
}

/// <summary>Identifies a credential in an external source without carrying the value.</summary>
/// <param name="Source">Resolver name, such as environment or file.</param>
/// <param name="Key">Variable name, absolute file path, or key understood by an application-supplied resolver.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MailCredentialReference(string Source, string Key);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MailDeliveryConfiguration))]
internal partial class MailDeliveryJsonContext : JsonSerializerContext;
