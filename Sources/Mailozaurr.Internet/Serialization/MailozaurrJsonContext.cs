using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>Source-generated metadata used by Mailozaurr for AOT-safe JSON serialization.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SmtpResult))]
[JsonSerializable(typeof(PendingMessageRecord))]
[JsonSerializable(typeof(PendingMessageDeadLetterRecord))]
[JsonSerializable(typeof(PendingMessageLogEnvelope))]
[JsonSerializable(typeof(SentMessageRecord))]
[JsonSerializable(typeof(OAuthCredential))]
[JsonSerializable(typeof(Dictionary<string, OAuthCredential>))]
[JsonSerializable(typeof(OAuthCredentialCacheEntry), TypeInfoPropertyName = "OAuthCredentialCacheEntry")]
[JsonSerializable(typeof(Dictionary<string, OAuthCredentialCacheEntry>), TypeInfoPropertyName = "DictionaryStringOAuthCredentialCacheEntry")]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, bool>), TypeInfoPropertyName = "DictionaryStringBool")]
[JsonSerializable(typeof(Dictionary<string, int>), TypeInfoPropertyName = "DictionaryStringInt")]
[JsonSerializable(typeof(Dictionary<string, double>), TypeInfoPropertyName = "DictionaryStringDouble")]
[JsonSerializable(typeof(Dictionary<string, long>), TypeInfoPropertyName = "DictionaryStringLong")]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, object?>), TypeInfoPropertyName = "DictionaryStringObjectNullable")]
[JsonSerializable(typeof(Dictionary<string, object>), TypeInfoPropertyName = "DictionaryStringObject")]
[JsonSerializable(typeof(Dictionary<string, JsonElement?>))]
[JsonSerializable(typeof(object), TypeInfoPropertyName = "Object")]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Attachment))]
[JsonSerializable(typeof(SendGridMessage))]
[JsonSerializable(typeof(SendGridPersonalization))]
[JsonSerializable(typeof(SendGridEmailAddress))]
[JsonSerializable(typeof(SendGridContent))]
[JsonSerializable(typeof(SendGridAttachment))]
[JsonSerializable(typeof(PendingMessageRepositoryOptions))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
[JsonSerializable(typeof(Dictionary<string, IList<string>>))]
public partial class MailozaurrJsonContext : JsonSerializerContext;
