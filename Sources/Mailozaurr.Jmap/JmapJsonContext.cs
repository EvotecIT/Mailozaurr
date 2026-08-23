using System.Text.Json.Serialization;

namespace Mailozaurr;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JmapSessionResource))]
[JsonSerializable(typeof(JmapMailboxQueryArguments))]
[JsonSerializable(typeof(JmapMailboxQueryResult))]
[JsonSerializable(typeof(JmapMailboxGetArguments))]
[JsonSerializable(typeof(JmapMailboxGetResponse))]
[JsonSerializable(typeof(JmapEmailQueryArguments))]
[JsonSerializable(typeof(JmapEmailQueryResult))]
[JsonSerializable(typeof(JmapEmailGetArguments))]
[JsonSerializable(typeof(JmapEmailGetResult))]
[JsonSerializable(typeof(JmapEmailChangesArguments))]
[JsonSerializable(typeof(JmapEmailChangesResult))]
[JsonSerializable(typeof(JmapThreadGetArguments))]
[JsonSerializable(typeof(JmapThreadGetResponse))]
[JsonSerializable(typeof(JmapIdentityGetArguments))]
[JsonSerializable(typeof(JmapIdentityGetResponse))]
internal partial class JmapJsonContext : JsonSerializerContext;
