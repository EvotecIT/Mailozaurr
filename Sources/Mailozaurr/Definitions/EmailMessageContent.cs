namespace Mailozaurr.Definitions;

using MimeKit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Represents transport-neutral message content that can be supplied to any Mailozaurr sender.
/// </summary>
/// <remarks>
/// The envelope (sender, recipients, authentication, and transport settings) intentionally remains
/// separate. Use <see cref="FromRenderResult"/> to adapt renderer results such as
/// <c>HtmlForgeX.Email.EmailRenderResult</c> without introducing a package dependency.
/// </remarks>
public sealed class EmailMessageContent {
    /// <summary>Gets or sets the message subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTML body.</summary>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain-text alternative body.</summary>
    public string TextBody { get; set; } = string.Empty;

    /// <summary>Gets the regular MIME attachments.</summary>
    public IList<AttachmentDescriptor> Attachments { get; } = new List<AttachmentDescriptor>();

    /// <summary>Gets the inline MIME resources referenced from the HTML body by content ID.</summary>
    public IList<AttachmentDescriptor> InlineAttachments { get; } = new List<AttachmentDescriptor>();

    /// <summary>Gets the custom message headers.</summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adapts a renderer result exposing <c>Html</c>, <c>PlainText</c>, <c>Subject</c>,
    /// <c>InlineResources</c>, and <c>Attachments</c> properties.
    /// </summary>
    /// <param name="renderResult">Renderer result or an existing <see cref="EmailMessageContent"/>.</param>
    /// <returns>Transport-neutral message content.</returns>
    public static EmailMessageContent FromRenderResult(object renderResult) {
        if (renderResult == null) {
            throw new ArgumentNullException(nameof(renderResult));
        }

        if (renderResult is EmailMessageContent content) {
            return content;
        }

        var type = renderResult.GetType();
        var htmlProperty = type.GetProperty("Html", BindingFlags.Instance | BindingFlags.Public)
            ?? type.GetProperty("HtmlBody", BindingFlags.Instance | BindingFlags.Public);
        var textProperty = type.GetProperty("PlainText", BindingFlags.Instance | BindingFlags.Public)
            ?? type.GetProperty("TextBody", BindingFlags.Instance | BindingFlags.Public);
        var subjectProperty = type.GetProperty("Subject", BindingFlags.Instance | BindingFlags.Public);

        if (htmlProperty == null && textProperty == null && subjectProperty == null) {
            throw new ArgumentException(
                $"Type '{type.FullName}' does not expose a supported email content shape.",
                nameof(renderResult));
        }

        var result = new EmailMessageContent {
            HtmlBody = ReadString(htmlProperty, renderResult),
            TextBody = ReadString(textProperty, renderResult),
            Subject = ReadString(subjectProperty, renderResult)
        };

        AddResources(type.GetProperty("InlineResources", BindingFlags.Instance | BindingFlags.Public)?.GetValue(renderResult), result.InlineAttachments, inline: true);
        AddResources(type.GetProperty("Attachments", BindingFlags.Instance | BindingFlags.Public)?.GetValue(renderResult), result.Attachments, inline: false);
        AddHeaders(type.GetProperty("Headers", BindingFlags.Instance | BindingFlags.Public)?.GetValue(renderResult), result.Headers);
        return result;
    }

    private static string ReadString(PropertyInfo? property, object source) =>
        property?.GetValue(source)?.ToString() ?? string.Empty;

    private static void AddResources(object? resources, IList<AttachmentDescriptor> destination, bool inline) {
        if (resources is not IEnumerable enumerable || resources is string) {
            return;
        }

        foreach (var resource in enumerable) {
            if (resource == null) {
                continue;
            }

            if (resource is AttachmentDescriptor descriptor) {
                destination.Add(descriptor);
                continue;
            }

            var type = resource.GetType();
            var data = type.GetProperty("Data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(resource) as byte[];
            if (data == null) {
                continue;
            }

            var contentId = ReadString(type.GetProperty("ContentId", BindingFlags.Instance | BindingFlags.Public), resource);
            var fileName = ReadString(type.GetProperty("FileName", BindingFlags.Instance | BindingFlags.Public), resource);
            if (string.IsNullOrWhiteSpace(fileName)) {
                fileName = inline && !string.IsNullOrWhiteSpace(contentId) ? contentId : "attachment";
            }

            var mimeType = ReadString(type.GetProperty("MimeType", BindingFlags.Instance | BindingFlags.Public), resource);
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
                AddHeader(entry.Key, entry.Value, destination);
            }
            return;
        }

        if (headers is not IEnumerable enumerable || headers is string) {
            return;
        }

        foreach (var entry in enumerable) {
            if (entry == null) continue;
            var type = entry.GetType();
            AddHeader(
                type.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry),
                type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry),
                destination);
        }
    }

    private static void AddHeader(object? key, object? value, IDictionary<string, string> destination) {
        var name = key?.ToString();
        if (!string.IsNullOrWhiteSpace(name) && value != null) {
            destination[name!] = value.ToString() ?? string.Empty;
        }
    }
}
