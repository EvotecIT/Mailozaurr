using Mailozaurr.Definitions;
using MimeKit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

public sealed partial class CmdletSendEmailMessage {
    /// <summary>
    /// <para>Specifies transport-neutral content created by Mailozaurr or returned by a compatible renderer.</para>
    /// <para>Explicit Subject, HTML, Text, Headers, Attachment, and InlineAttachment parameters override or extend this content.</para>
    /// </summary>
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ValueFromPipeline = true, ParameterSetName = "EmailProviders")]
    [Alias("EmailContent", "RenderResult")]
    public object? Content { get; set; }

    private void ApplyContent() {
        if (Content == null) {
            return;
        }

        var content = ConvertContent(Content);

        if (!MyInvocation.BoundParameters.ContainsKey(nameof(Subject))) {
            Subject = string.IsNullOrEmpty(content.Subject) ? null : content.Subject;
        }
        if (!MyInvocation.BoundParameters.ContainsKey(nameof(HTML))) {
            HTML = string.IsNullOrEmpty(content.HtmlBody) ? null : new[] { content.HtmlBody };
        }
        if (!MyInvocation.BoundParameters.ContainsKey(nameof(Text))) {
            Text = string.IsNullOrEmpty(content.TextBody) ? null : new[] { content.TextBody };
        }

        Attachment = MergeAttachments(content.Attachments, GetExplicitAttachments(nameof(Attachment)));
        InlineAttachment = MergeAttachments(content.InlineAttachments, GetExplicitAttachments(nameof(InlineAttachment)));

        Headers = CloneExplicitHeaders();
        if (content.Headers.Count > 0) {
            Headers ??= new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (var header in content.Headers) {
                if (!Headers.ContainsKey(header.Key)) {
                    Headers[header.Key] = header.Value;
                }
            }
        }
    }

    private static EmailMessageContent ConvertContent(object input) {
        var adapted = PSObject.AsPSObject(input);
        if (adapted.BaseObject is EmailMessageContent existing) {
            return existing;
        }

        var result = new EmailMessageContent {
            Subject = ReadString(adapted, "Subject"),
            HtmlBody = ReadString(adapted, "Html", "HtmlBody"),
            TextBody = ReadString(adapted, "PlainText", "TextBody")
        };
        var hasSupportedShape = HasProperty(adapted, "Subject", "Html", "HtmlBody", "PlainText", "TextBody", "InlineResources", "Attachments", "Headers");
        if (!hasSupportedShape) {
            throw new ArgumentException(
                $"Type '{adapted.BaseObject.GetType().FullName}' does not expose a supported email content shape.",
                nameof(input));
        }

        AddResources(ReadValue(adapted, "InlineResources"), result.InlineAttachments, inline: true);
        AddResources(ReadValue(adapted, "Attachments"), result.Attachments, inline: false);
        AddHeaders(ReadValue(adapted, "Headers"), result.Headers);
        return result;
    }

    private static bool HasProperty(PSObject source, params string[] names) =>
        names.Any(name => source.Properties[name] != null);

    private static object? ReadValue(PSObject source, params string[] names) {
        foreach (var name in names) {
            var property = source.Properties[name];
            if (property != null) return property.Value;
        }
        return null;
    }

    private static string ReadString(PSObject source, params string[] names) =>
        ReadValue(source, names)?.ToString() ?? string.Empty;

    private static void AddResources(object? resources, IList<AttachmentDescriptor> destination, bool inline) {
        if (resources is not IEnumerable enumerable || resources is string) return;

        foreach (var resource in enumerable) {
            if (resource == null) continue;
            var adapted = PSObject.AsPSObject(resource);
            if (adapted.BaseObject is AttachmentDescriptor descriptor) {
                destination.Add(descriptor);
                continue;
            }

            var data = ReadValue(adapted, "Data", "Bytes") as byte[];
            if (data == null) continue;
            var contentId = ReadString(adapted, "ContentId");
            var fileName = ReadString(adapted, "FileName", "Name");
            if (string.IsNullOrWhiteSpace(fileName)) {
                fileName = inline && !string.IsNullOrWhiteSpace(contentId) ? contentId : "attachment";
            }
            var mimeType = ReadString(adapted, "MimeType", "ContentType");
            destination.Add(new ByteArrayAttachmentDescriptor(data, fileName) {
                ContentType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType,
                ContentId = string.IsNullOrWhiteSpace(contentId) ? null : contentId,
                ContentDisposition = new ContentDisposition(inline ? ContentDisposition.Inline : ContentDisposition.Attachment)
            });
        }
    }

    private static void AddHeaders(object? headers, IDictionary<string, string> destination) {
        if (headers is IDictionary dictionary) {
            foreach (DictionaryEntry entry in dictionary) {
                var key = entry.Key?.ToString();
                if (!string.IsNullOrWhiteSpace(key) && entry.Value != null) destination[key!] = entry.Value.ToString() ?? string.Empty;
            }
            return;
        }
        if (headers == null) return;

        foreach (var property in PSObject.AsPSObject(headers).Properties) {
            if (!string.IsNullOrWhiteSpace(property.Name) && property.Value != null) {
                destination[property.Name] = property.Value.ToString() ?? string.Empty;
            }
        }
    }

    private static object[]? MergeAttachments(IEnumerable<AttachmentDescriptor> contentAttachments, object[]? explicitAttachments) {
        var merged = contentAttachments.Cast<object>().ToList();
        if (explicitAttachments != null) {
            merged.AddRange(explicitAttachments.Where(item => item != null));
        }
        return merged.Count == 0 ? null : merged.ToArray();
    }

    private object[]? GetExplicitAttachments(string parameterName) =>
        MyInvocation.BoundParameters.TryGetValue(parameterName, out var value) ? value as object[] : null;

    private Hashtable? CloneExplicitHeaders() {
        if (!MyInvocation.BoundParameters.TryGetValue(nameof(Headers), out var value) || value is not IDictionary source) {
            return null;
        }

        var clone = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in source) {
            clone[entry.Key] = entry.Value;
        }
        return clone;
    }
}
