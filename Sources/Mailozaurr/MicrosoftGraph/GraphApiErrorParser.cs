using System;
using System.Net;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace Mailozaurr;

/// <summary>
/// Parses raw Graph API error messages into structured objects.
/// </summary>
public static class GraphApiErrorParser {
    /// <summary>
    /// Parses a raw error string returned by Graph API.
    /// </summary>
    /// <param name="message">Raw error message.</param>
    /// <returns>Parsed <see cref="GraphApiErrorResponse"/> or <c>null</c> if parsing fails.</returns>
    public static GraphApiErrorResponse? Parse(string? message) {
        if (string.IsNullOrWhiteSpace(message)) {
            return null;
        }

        try {
            var lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) {
                return null;
            }

            var response = new GraphApiErrorResponse();
            var index = 0;

            var first = lines[index++];
            var firstParts = first.Split(new[] { ' ' }, 2);
            if (firstParts.Length != 2 || !Enum.TryParse<GraphHttpMethod>(firstParts[0], true, out var method)) {
                return null;
            }
            response.Method = method;
            response.Uri = firstParts[1];

            if (index < lines.Length) {
                var second = lines[index++];
                var match = Regex.Match(second, @"HTTP/\S+\s+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var status)) {
                    response.StatusCode = (HttpStatusCode)status;
                }
            }

            for (; index < lines.Length; index++) {
                var line = lines[index];
                if (line.StartsWith("{", StringComparison.Ordinal)) {
                    var body = string.Join(Environment.NewLine, lines, index, lines.Length - index);
                    try {
                        var error = JsonSerializer.Deserialize<GraphApiError>(body);
                        response.Error = error?.Error;
                    } catch {
                        // ignore
                    }
                    break;
                }

                var separator = line.IndexOf(':');
                if (separator <= 0) {
                    continue;
                }

                var key = line.Substring(0, separator);
                var value = line.Substring(separator + 1).Trim();
                switch (key.ToLowerInvariant()) {
                    case "cache-control":
                        response.Headers.CacheControl = value;
                        break;
                    case "strict-transport-security":
                        response.Headers.StrictTransportSecurity = value;
                        break;
                    case "request-id":
                        response.Headers.RequestId = value;
                        break;
                    case "client-request-id":
                        response.Headers.ClientRequestId = value;
                        break;
                    case "date":
                        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt)) {
                            response.Headers.Date = dt;
                        }
                        break;
                    case "x-ms-ags-diagnostic":
                        try {
                            response.Headers.Diagnostic = JsonSerializer.Deserialize<GraphApiDiagnostic>(value);
                        } catch {
                            // ignore
                        }
                        break;
                    default:
                        try {
                            var json = JsonSerializer.Deserialize<JsonElement>(value);
                            response.Headers.AdditionalJsonHeaders[key] = json;
                        } catch {
                            response.Headers.AdditionalHeaders[key] = value;
                        }
                        break;
                }
            }

            return response;
        } catch {
            return null;
        }
    }
}