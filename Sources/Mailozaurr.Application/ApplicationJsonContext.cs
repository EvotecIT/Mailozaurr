using System.Text.Json.Serialization;

namespace Mailozaurr.Application;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(MailDraft))]
[JsonSerializable(typeof(MessageActionExecutionPlan))]
[JsonSerializable(typeof(IReadOnlyList<MessageActionExecutionPlan>), TypeInfoPropertyName = "MessageActionExecutionPlanList")]
[JsonSerializable(typeof(MailDraftStoreDocument))]
[JsonSerializable(typeof(MailMessageActionPlanBatchStoreDocument))]
[JsonSerializable(typeof(MailProfileStoreDocument))]
[JsonSerializable(typeof(MailSecretStoreDocument))]
internal partial class ApplicationJsonContext : JsonSerializerContext;

internal sealed class MailDraftStoreDocument {
    public int Version { get; set; } = 1;

    public List<MailDraft> Drafts { get; set; } = new();
}

internal sealed class MailMessageActionPlanBatchStoreDocument {
    public int Version { get; set; } = 1;

    public List<MailMessageActionPlanBatch> Batches { get; set; } = new();
}

internal sealed class MailProfileStoreDocument {
    public int Version { get; set; } = 1;

    public List<MailProfile> Profiles { get; set; } = new();
}

internal sealed class MailSecretStoreDocument {
    public int Version { get; set; } = 2;

    public Dictionary<string, Dictionary<string, string>> ProfileSecrets { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Secrets { get; set; }
}
