using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.IO;

namespace Mailozaurr {
    /// <summary>
    /// Represents credentials required for Microsoft Graph authentication.
    /// </summary>
    public class GraphCredential {
        public string ClientId { get; set; }
        public string DirectoryId { get; set; }
        public string ClientSecret { get; set; }
    }

    /// <summary>
    /// Simplified representation of an email message returned from Graph.
    /// </summary>
    public class GraphEmailMessage {
        public string Id { get; set; }
        public string Subject { get; set; }
        public string BodyPreview { get; set; }
        public object Body { get; set; }
        public string ChangeKey { get; set; }
        // Add more properties as needed
    }

    /// <summary>
    /// Represents a file attachment from Microsoft Graph.
    /// </summary>
    public class Attachment {
        public string Name { get; set; }
        public string ContentBytes { get; set; }
        // Add more properties as needed
    }

    public static class MicrosoftGraphUtils {
        private static readonly HttpClient HttpClient;

        static MicrosoftGraphUtils() {
            HttpClient = new HttpClient();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => HttpClient.Dispose();
        }
        /// <summary>
        /// Converts a credential string (username@directory) and secret to a GraphCredential object.
        /// </summary>
        public static GraphCredential ConvertFromGraphCredential(string username, string password) {
            var parts = username.Split('@');
            if (parts.Length != 2) {
                throw new ArgumentException("Invalid credential format. Expected 'clientid@directoryid'.");
            }

            return new GraphCredential {
                ClientId = parts[0],
                DirectoryId = parts[1],
                ClientSecret = password
            };
        }

        /// <summary>
        /// Connects to O365 Graph and returns the Authorization header value ("Bearer ...").
        /// </summary>
        public static async Task<string> ConnectO365GraphAsync(GraphCredential credential, string tenantDomain, string resource = "https://manage.office.com") {
            var body = new Dictionary<string, string>
            {
                {"grant_type", "client_credentials"},
                {"resource", resource},
                {"client_id", credential.ClientId},
                {"client_secret", credential.ClientSecret}
            };
            var content = new FormUrlEncodedContent(body);
            var url = $"https://login.microsoftonline.com/{tenantDomain}/oauth2/token";
            var response = await HttpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"ConnectO365GraphAsync - Error: {error}");
            }
            var json = await response.Content.ReadAsStringAsync();
                // Parse JSON for access_token and token_type
                var token = System.Text.Json.JsonDocument.Parse(json);
                var accessToken = token.RootElement.GetProperty("access_token").GetString();
                var tokenType = token.RootElement.GetProperty("token_type").GetString();
            return $"{tokenType} {accessToken}";
        }

        /// <summary>
        /// Builds a full URI from base, path, and query parameters.
        /// </summary>
        public static string BuildGraphUri(string baseUri, string path, IDictionary<string, string> queryParameters = null) {
            var uriBuilder = new StringBuilder();
            uriBuilder.Append(baseUri.TrimEnd('/'));
            if (!string.IsNullOrEmpty(path)) {
                if (!path.StartsWith("/")) uriBuilder.Append('/');
                uriBuilder.Append(path);
            }
            if (queryParameters != null && queryParameters.Count > 0) {
                var first = true;
                foreach (var kvp in queryParameters) {
                    if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
                    uriBuilder.Append(first ? '?' : '&');
                    uriBuilder.Append(Uri.EscapeDataString(kvp.Key));
                    uriBuilder.Append('=');
                    uriBuilder.Append(Uri.EscapeDataString(kvp.Value));
                    first = false;
                }
            }
            return uriBuilder.ToString();
        }

        /// <summary>
        /// Invokes a REST API call to Microsoft Graph and returns the result as a JsonDocument.
        /// </summary>
        public static async Task<JsonDocument> InvokeGraphApiAsync(
            string method,
            string uri,
            IDictionary<string, string> headers = null,
            string body = null) {
            var request = new HttpRequestMessage(new HttpMethod(method), uri);
            if (headers != null) {
                foreach (var kvp in headers) {
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }
            if (!string.IsNullOrEmpty(body) && (method == "POST" || method == "PUT" || method == "PATCH")) {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
            using var response = await HttpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                throw new Exception($"InvokeGraphApiAsync - Error: {response.StatusCode} - {responseContent}");
            }
            return JsonDocument.Parse(responseContent);
        }

        /// <summary>
        /// Removes empty values from a dictionary recursively (null, empty string, empty array, empty dictionary).
        /// </summary>
        public static void RemoveEmptyValues(IDictionary<string, object> dict, HashSet<string> exclude = null, bool recursive = true, int rerun = 0) {
            exclude ??= new HashSet<string>();
            var keys = dict.Keys.ToList();
            foreach (var key in keys) {
                if (exclude.Contains(key)) continue;
                var value = dict[key];
                if (recursive && value is IDictionary<string, object> subDict) {
                    if (subDict.Count == 0) {
                        dict.Remove(key);
                    } else {
                        RemoveEmptyValues(subDict, exclude, recursive, rerun);
                        if (subDict.Count == 0) dict.Remove(key);
                    }
                } else if (value == null) {
                    dict.Remove(key);
                } else if (value is string s && s == string.Empty) {
                    dict.Remove(key);
                } else if (value is System.Collections.IList list && list.Count == 0) {
                    dict.Remove(key);
                } else if (value is IDictionary<string, object> d && d.Count == 0) {
                    dict.Remove(key);
                }
            }
            if (rerun > 0) {
                for (int i = 0; i < rerun; i++) {
                    RemoveEmptyValues(dict, exclude, recursive, 0);
                }
            }
        }

        /// <summary>
        /// Joins a base URI, optional relative URI, and query parameters into a full URI string.
        /// </summary>
        public static string JoinUriQuery(string baseUri, string relativeOrAbsoluteUri = null, IDictionary<string, object> queryParameters = null, bool escapeUriString = false) {
            string url = baseUri.TrimEnd('/');
            if (!string.IsNullOrEmpty(relativeOrAbsoluteUri)) {
                url += "/" + relativeOrAbsoluteUri.TrimStart('/');
            }
            var uriBuilder = new UriBuilder(url);
            if (queryParameters != null && queryParameters.Count > 0) {
                var query = new StringBuilder();
                bool first = true;
                foreach (var kvp in queryParameters) {
                    if (kvp.Value == null) continue;
                    if (!first) query.Append('&');
                    query.Append(Uri.EscapeDataString(kvp.Key));
                    query.Append('=');
                    query.Append(Uri.EscapeDataString(kvp.Value.ToString()));
                    first = false;
                }
                uriBuilder.Query = query.ToString();
            }
            var result = uriBuilder.Uri.AbsoluteUri;
            if (escapeUriString) {
                result = Uri.EscapeUriString(result);
            }
            return result;
        }

        private static object ConvertJsonElementToNativeObject(JsonElement element) {
            switch (element.ValueKind) {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                        dict[prop.Name] = ConvertJsonElementToNativeObject(prop.Value);
                    return dict;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                        list.Add(ConvertJsonElementToNativeObject(item));
                    return list.ToArray();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var l)) return l;
                    if (element.TryGetDouble(out var d)) return d;
                    return element.GetRawText();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        /// <summary>
        /// Retrieves mail messages for the specified user.
        /// </summary>
        public static async Task<List<Dictionary<string, object>>> GetMailMessagesAsync(GraphCredential credential, string userPrincipalName, IEnumerable<string> properties = null, string filter = null, int? limit = null) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var queryParams = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(filter)) queryParams["$filter"] = filter;
            if (properties != null && properties.Any()) queryParams["$select"] = string.Join(",", properties);
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages", queryParams);
            var doc = await InvokeGraphApiAsync("GET", uri, headers);
            var messages = new List<Dictionary<string, object>>();
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    var native = ConvertJsonElementToNativeObject(item) as Dictionary<string, object>;
                    messages.Add(native);
                    if (limit.HasValue && messages.Count >= limit.Value) break;
                }
            }
            return messages;
        }

        /// <summary>
        /// Retrieves attachments for a specific message.
        /// </summary>
        public static async Task<List<Attachment>> GetMailMessageAttachmentsAsync(GraphCredential credential, string userPrincipalName, string messageId, IEnumerable<string> properties = null) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var queryParams = new Dictionary<string, object>();
            if (properties != null && properties.Any()) queryParams["$select"] = string.Join(",", properties);
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages/{messageId}/attachments", queryParams);
            var doc = await InvokeGraphApiAsync("GET", uri, headers);
            var attachments = new List<Attachment>();
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    var att = JsonSerializer.Deserialize<Attachment>(item.GetRawText());
                    attachments.Add(att);
                }
            }
            return attachments;
        }

        /// <summary>
        /// Lists mail folders for the specified user.
        /// </summary>
        public static async Task<List<JsonElement>> GetMailFoldersAsync(GraphCredential credential, string userPrincipalName) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/mailFolders");
            var doc = await InvokeGraphApiAsync("GET", uri, headers);
            var folders = new List<JsonElement>();
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    folders.Add(item);
                }
            }
            return folders;
        }

        /// <summary>
        /// Saves the bodies of messages to disk as HTML files.
        /// </summary>
        public static void SaveMailMessages(IEnumerable<GraphEmailMessage> messages, string path) {
            var resolvedPath = Path.GetFullPath(path);
            if (!Directory.Exists(resolvedPath)) Directory.CreateDirectory(resolvedPath);
            foreach (var m in messages) {
                if (m?.Body is not null) {
                    var randomFileName = Path.ChangeExtension(Path.GetRandomFileName(), "html");
                    var filePath = Path.Combine(resolvedPath, randomFileName);
                    try {
                        var content = m.Body is JsonElement je && je.TryGetProperty("Content", out var c) ? c.GetString() : m.Body.ToString();
                        File.WriteAllText(filePath, content);
                    } catch (Exception ex) {
                        // Log or handle error
                        LoggingMessages.Logger.WriteWarning($"SaveMailMessage - Couldn't save file to {filePath}. Error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Saves attachments to the specified directory.
        /// </summary>
        public static void SaveAttachments(IEnumerable<Attachment> attachments, string path) {
            var resolvedPath = Path.GetFullPath(path);
            if (!Directory.Exists(resolvedPath)) Directory.CreateDirectory(resolvedPath);
            foreach (var att in attachments) {
                if (!string.IsNullOrEmpty(att.ContentBytes) && !string.IsNullOrEmpty(att.Name)) {
                    var filePath = Path.Combine(resolvedPath, att.Name);
                    try {
                        var bytes = Convert.FromBase64String(att.ContentBytes);
                        File.WriteAllBytes(filePath, bytes);
                    } catch (Exception ex) {
                        // Log or handle error
                        LoggingMessages.Logger.WriteWarning($"SaveAttachment - Couldn't save file to {filePath}. Error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Moves a mail message to another folder.
        /// </summary>
        public static async Task MoveMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, string destinationFolderId) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages/{messageId}/move");
            var body = JsonSerializer.Serialize(new { destinationId = destinationFolderId });
            await InvokeGraphApiAsync("POST", uri, headers, body);
        }

        /// <summary>
        /// Sets the read state for a mail message.
        /// </summary>
        public static async Task SetMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, bool isRead) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages/{messageId}");
            var body = JsonSerializer.Serialize(new { isRead });
            await InvokeGraphApiAsync("PATCH", uri, headers, body);
        }

        /// <summary>
        /// Deletes a mail message.
        /// </summary>
        public static async Task DeleteMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages/{messageId}");
            await InvokeGraphApiAsync("DELETE", uri, headers);
        }
    }
}