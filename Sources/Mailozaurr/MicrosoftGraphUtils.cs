using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.IO;
using System.Collections.Concurrent;

namespace Mailozaurr {


    public static class MicrosoftGraphUtils {
        private static readonly HttpClient HttpClient;
        private static readonly ConcurrentDictionary<string, GraphAuthorization> TokenCache = new();

        /// <summary>
        /// Timeout for HTTP operations in seconds.
        /// </summary>
        public static int TimeoutSeconds {
            get => (int)HttpClient.Timeout.TotalSeconds;
            set => HttpClient.Timeout = TimeSpan.FromSeconds(value);
        }

        static MicrosoftGraphUtils() {
            HttpClient = new HttpClient();
            HttpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
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
            var key = $"{credential.ClientId}|{tenantDomain}|{credential.CertificatePath}|{credential.ClientSecret}|{resource}";
            if (TokenCache.TryGetValue(key, out var cached) && cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5)) {
                return $"{cached.TokenType} {cached.AccessToken}";
            }
            var cachedFile = OAuthTokenCache.Get($"graph:{key}");
            if (cachedFile != null && cachedFile.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5)) {
                TokenCache[key] = new GraphAuthorization { AccessToken = cachedFile.AccessToken, TokenType = "Bearer", ExpiresOn = cachedFile.ExpiresOn };
                return $"Bearer {cachedFile.AccessToken}";
            }
            if (!string.IsNullOrWhiteSpace(credential.CertificatePath)) {
                var scopes = new[] { $"{resource}/.default" };
                var auth = await OAuthHelpers.AcquireGraphCertificateTokenAsync(
                    credential.ClientId,
                    tenantDomain,
                    credential.CertificatePath,
                    credential.CertificatePassword ?? string.Empty,
                    scopes);
                TokenCache[key] = auth;
                return $"{auth.TokenType} {auth.AccessToken}";
            }
            if (credential.CertificateBytes != null) {
                var scopes = new[] { $"{resource}/.default" };
                var auth = await OAuthHelpers.AcquireGraphCertificateTokenAsync(
                    credential.ClientId,
                    tenantDomain,
                    credential.CertificateBytes,
                    credential.CertificatePassword ?? string.Empty,
                    scopes);
                return $"{auth.TokenType} {auth.AccessToken}";
            }
            if (!string.IsNullOrWhiteSpace(credential.CertificatePemPath)) {
                var scopes = new[] { $"{resource}/.default" };
                var auth = await OAuthHelpers.AcquireGraphCertificatePemTokenAsync(
                    credential.ClientId,
                    tenantDomain,
                    credential.CertificatePemPath,
                    scopes);
                return $"{auth.TokenType} {auth.AccessToken}";
            }

            var body = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "resource", resource },
                { "client_id", credential.ClientId },
                { "client_secret", credential.ClientSecret }
            };
            var content = new FormUrlEncodedContent(body);
            var url = $"https://login.microsoftonline.com/{tenantDomain}/oauth2/token";
            using var response = await HttpClient.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                throw new GraphApiException(
                    response.StatusCode,
                    $"ConnectO365GraphAsync - Error: {json}",
                    json);
            }
            var token = System.Text.Json.JsonDocument.Parse(json);
            var accessToken = token.RootElement.GetProperty("access_token").GetString();
            var tokenType = token.RootElement.GetProperty("token_type").GetString();
            var expiresOn = DateTimeOffset.UtcNow.AddHours(1);
            if (token.RootElement.TryGetProperty("expires_in", out var expIn)) {
                expiresOn = DateTimeOffset.UtcNow.AddSeconds(expIn.GetInt32());
            }
            if (token.RootElement.TryGetProperty("expires_on", out var expOn)) {
                if (long.TryParse(expOn.GetString(), out var expSeconds)) {
                    expiresOn = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                }
            }
            TokenCache[key] = new GraphAuthorization { AccessToken = accessToken, TokenType = tokenType, ExpiresOn = expiresOn };
            OAuthTokenCache.Set($"graph:{key}", new OAuthCredential {
                UserName = credential.ClientId,
                AccessToken = accessToken,
                ExpiresOn = expiresOn
            });
            return $"{tokenType} {accessToken}";
        }

        /// <summary>
        /// Connects to O365 Graph with retry logic and returns the Authorization header value.
        /// </summary>
        public static async Task<string> ConnectO365GraphWithRetryAsync(
            GraphCredential credential,
            string tenantDomain,
            int retryCount,
            int retryDelayMilliseconds,
            double retryDelayBackoff,
            string resource = "https://manage.office.com") {
            int attempts = 0;
            Exception? lastException = null;
            do {
                try {
                    return await ConnectO365GraphAsync(credential, tenantDomain, resource);
                } catch (Exception ex) {
                    lastException = ex;
                    LoggingMessages.Logger.WriteWarning($"Connect-EmailGraph - {ex.Message}");
                    if ((!Helpers.IsTransient(ex)) || attempts >= retryCount) {
                        throw;
                    }
                    var delay = (int)Math.Round(retryDelayMilliseconds * Math.Pow(retryDelayBackoff, attempts));
                    if (delay > 0) {
                        await Task.Delay(delay);
                    }
                }
                attempts++;
            } while (attempts <= retryCount);
            throw lastException!;
        }

        /// <summary>
        /// Builds a full URI from base, path, and query parameters.
        /// </summary>
        public static string BuildGraphUri(string baseUri, string path, IDictionary<string, string> queryParameters = null) {
            var uriBuilder = new StringBuilder();
            uriBuilder.Append(baseUri.TrimEnd('/'));
            if (!string.IsNullOrWhiteSpace(path)) {
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
            if (!string.IsNullOrWhiteSpace(body) && (method == "POST" || method == "PUT" || method == "PATCH")) {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
            using var response = await HttpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                throw new GraphApiException(
                    response.StatusCode,
                    $"InvokeGraphApiAsync - Error: {response.StatusCode} - {responseContent}",
                    responseContent);
            }
            return JsonDocument.Parse(responseContent);
        }

        /// <summary>
        /// Sends multiple requests to Microsoft Graph in a single batch.
        /// </summary>
        public static async Task<IReadOnlyList<GraphBatchResult>> SendBatchAsync(GraphCredential credential, IEnumerable<GraphBatchRequest> requests) {
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            var headers = new Dictionary<string, string> { { "Authorization", token } };
            var batchPayload = new { requests = requests.Select(r => new { id = r.Id, method = r.Method, url = r.Url.TrimStart('/') , headers = r.Headers, body = r.Body }) };
            var jsonBody = JsonSerializer.Serialize(batchPayload);
            var doc = await InvokeGraphApiAsync("POST", "https://graph.microsoft.com/v1.0/$batch", headers, jsonBody);
            var results = new List<GraphBatchResult>();
            if (doc.RootElement.TryGetProperty("responses", out var responses) && responses.ValueKind == JsonValueKind.Array) {
                foreach (var item in responses.EnumerateArray()) {
                    var result = new GraphBatchResult();
                    if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String) result.Id = idEl.GetString();
                    if (item.TryGetProperty("status", out var statusEl) && statusEl.TryGetInt32(out var status)) result.Status = status;
                    if (item.TryGetProperty("headers", out var headersEl) && headersEl.ValueKind == JsonValueKind.Object) {
                        var h = new Dictionary<string, string>();
                        foreach (var prop in headersEl.EnumerateObject()) {
                            if (prop.Value.ValueKind == JsonValueKind.String) h[prop.Name] = prop.Value.GetString();
                        }
                        result.Headers = h;
                    }
                    if (item.TryGetProperty("body", out var bodyEl)) {
                        result.Body = bodyEl;
                    }
                    results.Add(result);
                }
            }
            return results;
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
                } else if (value is string s && string.IsNullOrWhiteSpace(s)) {
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
            if (!string.IsNullOrWhiteSpace(relativeOrAbsoluteUri)) {
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
            if (!string.IsNullOrWhiteSpace(filter)) queryParams["$filter"] = filter;
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
        public static void SaveMailMessages(IEnumerable<EmailGraphMessage> messages, string path) {
            var resolvedPath = Path.GetFullPath(path);
            if (!Directory.Exists(resolvedPath)) Directory.CreateDirectory(resolvedPath);
            foreach (var m in messages) {
                if (m?.Body is not null) {
                    var randomFileName = Path.ChangeExtension(Path.GetRandomFileName(), "html");
                    var filePath = Path.Combine(resolvedPath, randomFileName);
                    try {
                        var content = m.Body is JsonElement je && je.TryGetProperty("Content", out var c) ? c.GetString() : m.Body.ToString();
                        File.WriteAllText(filePath, content);
                    } catch (IOException ex) {
                        // Log or handle error
                        LoggingMessages.Logger.WriteWarning($"SaveMailMessage - Couldn't save file to {filePath}. Error: {ex.Message}");
                        LoggingMessages.Logger.WriteWarning($"SaveMailMessage - Possible issue: Ensure the directory '{resolvedPath}' exists and you have write permissions.");
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
                if (!string.IsNullOrWhiteSpace(att.ContentBytes) && !string.IsNullOrWhiteSpace(att.Name)) {
                    var filePath = Path.Combine(resolvedPath, att.Name);
                    try {
                        var bytes = Convert.FromBase64String(att.ContentBytes);
                        File.WriteAllBytes(filePath, bytes);
                    } catch (FormatException fex) {
                        // Invalid Base64 content
                        LoggingMessages.Logger.WriteWarning($"SaveAttachment - Invalid base64 content for {att.Name}. Error: {fex.Message}");
                        LoggingMessages.Logger.WriteWarning($"SaveAttachment - Possible issue: The attachment '{att.Name}' may be corrupted.");
                    } catch (IOException ex) {
                        // Log or handle other errors
                        LoggingMessages.Logger.WriteWarning($"SaveAttachment - Couldn't save file to {filePath}. Error: {ex.Message}");
                        LoggingMessages.Logger.WriteWarning($"SaveAttachment - Possible issue: Verify the path '{filePath}' exists and you have write permissions.");
                    }
                }
            }
        }

        /// <summary>
        /// Executes a search query across one or more mailboxes.
        /// </summary>
        public static async Task<List<GraphMessageInfo>> SearchMailboxesAsync(
            GraphCredential credential,
            IEnumerable<string> userPrincipalNames,
            string queryString,
            int from = 0,
            int size = 25) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;

            var requests = new List<object>();
            foreach (var upn in userPrincipalNames) {
                requests.Add(new {
                    entityTypes = new[] { "message" },
                    from,
                    size,
                    query = new { queryString },
                    userScopes = new[] { upn }
                });
            }

            var body = JsonSerializer.Serialize(new { requests });
            var doc = await InvokeGraphApiAsync("POST", "https://graph.microsoft.com/v1.0/search/query", headers, body);

            var results = new List<GraphMessageInfo>();
            int index = 0;
            if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                foreach (var item in valueElement.EnumerateArray()) {
                    var upn = userPrincipalNames.ElementAt(index++);
                    if (item.TryGetProperty("hitsContainers", out var containers) && containers.ValueKind == JsonValueKind.Array) {
                        foreach (var container in containers.EnumerateArray()) {
                            if (container.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array) {
                                foreach (var hit in hits.EnumerateArray()) {
                                    string? summary = null;
                                    if (hit.TryGetProperty("summary", out var sumEl)) summary = sumEl.GetString();
                                    if (hit.TryGetProperty("resource", out var res) && res.ValueKind == JsonValueKind.Object) {
                                        var dict = ConvertJsonElementToNativeObject(res) as Dictionary<string, object>;
                                        if (dict != null) {
                                            results.Add(new GraphMessageInfo(dict, upn, summary));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Performs an action on a mail message.
        /// </summary>
        public static async Task ExecuteMailMessageActionAsync(GraphCredential credential, string userPrincipalName, string messageId, GraphMessageAction action, string? destinationFolderId = null) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;

            string method;
            string uri;
            string? body = null;

            switch (action) {
                case GraphMessageAction.Move:
                    if (string.IsNullOrWhiteSpace(destinationFolderId)) throw new ArgumentNullException(nameof(destinationFolderId));
                    method = "POST";
                    uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages/{messageId}/move");
                    body = JsonSerializer.Serialize(new { destinationId = destinationFolderId });
                    break;
                case GraphMessageAction.Copy:
                    if (string.IsNullOrWhiteSpace(destinationFolderId)) throw new ArgumentNullException(nameof(destinationFolderId));
                    method = "POST";
                    uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages/{messageId}/copy");
                    body = JsonSerializer.Serialize(new { destinationId = destinationFolderId });
                    break;
                case GraphMessageAction.Delete:
                    method = "DELETE";
                    uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/messages/{messageId}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }

            await InvokeGraphApiAsync(method, uri, headers, body);
        }

        /// <summary>
        /// Moves a mail message to another folder.
        /// </summary>
        public static async Task MoveMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, string destinationFolderId) {
            await ExecuteMailMessageActionAsync(credential, userPrincipalName, messageId, GraphMessageAction.Move, destinationFolderId);
        }

        /// <summary>
        /// Copies a mail message to another folder.
        /// </summary>
        public static async Task CopyMailMessageAsync(GraphCredential credential, string userPrincipalName, string messageId, string destinationFolderId) {
            await ExecuteMailMessageActionAsync(credential, userPrincipalName, messageId, GraphMessageAction.Copy, destinationFolderId);
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
            await ExecuteMailMessageActionAsync(credential, userPrincipalName, messageId, GraphMessageAction.Delete);
        }

        /// <summary>
        /// Deletes all messages from the Junk Email folder.
        /// </summary>
        public static async Task ClearJunkMailAsync(
            GraphCredential credential,
            string userPrincipalName,
            IEnumerable<string>? skipIds = null,
            IEnumerable<string>? skipFrom = null,
            IEnumerable<string>? skipTo = null,
            IEnumerable<string>? skipSubjectContains = null,
            bool skipHasAttachment = false,
            IEnumerable<string>? skipAttachmentExtension = null) {
            var properties = new List<string> { "id" };
            if (skipFrom != null) properties.Add("from");
            if (skipTo != null) properties.Add("toRecipients");
            if (skipSubjectContains != null) properties.Add("subject");
            if (skipHasAttachment || skipAttachmentExtension != null) properties.Add("hasAttachments");

            var messages = await GetJunkMailMessagesAsync(
                credential,
                userPrincipalName,
                properties,
                skipIds,
                skipFrom,
                skipTo,
                skipSubjectContains,
                skipHasAttachment,
                skipAttachmentExtension);

            foreach (var msg in messages) {
                var id = msg["id"] as string;
                if (string.IsNullOrWhiteSpace(id)) continue;
                await DeleteMailMessageAsync(credential, userPrincipalName, id);
            }
        }

        public static List<Dictionary<string, object>> FilterJunkMessages(
            IEnumerable<Dictionary<string, object>> messages,
            IEnumerable<string>? skipIds = null,
            IEnumerable<string>? skipFrom = null,
            IEnumerable<string>? skipTo = null,
            IEnumerable<string>? skipSubjectContains = null,
            bool skipHasAttachment = false) {
            var result = new List<Dictionary<string, object>>();
            var skipIdsSet = skipIds != null ? new HashSet<string>(skipIds, StringComparer.OrdinalIgnoreCase) : null;
            foreach (var msg in messages) {
                var id = msg.TryGetValue("id", out var idObj) ? idObj as string : null;
                if (!string.IsNullOrWhiteSpace(id) && skipIdsSet != null && skipIdsSet.Contains(id)) {
                    continue;
                }

                if (skipFrom != null &&
                    msg.TryGetValue("from", out var fromObj) &&
                    fromObj is Dictionary<string, object> fDict &&
                    fDict.TryGetValue("emailAddress", out var addrObj) &&
                    addrObj is Dictionary<string, object> addr &&
                    addr.TryGetValue("address", out var fromAddrObj) &&
                    fromAddrObj is string fromAddr &&
                    skipFrom.Contains(fromAddr, StringComparer.OrdinalIgnoreCase)) {
                    continue;
                }

                if (skipTo != null &&
                    msg.TryGetValue("toRecipients", out var toObj) &&
                    toObj is object[] arr &&
                    arr.OfType<Dictionary<string, object>>().Any(rec =>
                        rec.TryGetValue("emailAddress", out var tAddrObj) &&
                        tAddrObj is Dictionary<string, object> tAddr &&
                        tAddr.TryGetValue("address", out var addrVal) &&
                        addrVal is string addrStr &&
                        skipTo.Contains(addrStr, StringComparer.OrdinalIgnoreCase))) {
                    continue;
                }

                if (skipSubjectContains != null &&
                    msg.TryGetValue("subject", out var subjObj) &&
                    subjObj is string subj &&
                    skipSubjectContains.Any(s => subj.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) {
                    continue;
                }

                if (skipHasAttachment &&
                    msg.TryGetValue("hasAttachments", out var hasObj) &&
                    hasObj is bool hasAtt && hasAtt) {
                    continue;
                }

                result.Add(msg);
            }
            return result;
        }

        /// <summary>
        /// Retrieves messages from the Junk Email folder.
        /// </summary>
        public static async Task<List<Dictionary<string, object>>> GetJunkMailMessagesAsync(
            GraphCredential credential,
            string userPrincipalName,
            IEnumerable<string>? properties = null,
            IEnumerable<string>? skipIds = null,
            IEnumerable<string>? skipFrom = null,
            IEnumerable<string>? skipTo = null,
            IEnumerable<string>? skipSubjectContains = null,
            bool skipHasAttachment = false,
            IEnumerable<string>? skipAttachmentExtension = null) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var props = properties != null ? new List<string>(properties) : new List<string>();
            if (skipHasAttachment || skipAttachmentExtension != null) {
                if (!props.Contains("hasAttachments")) props.Add("hasAttachments");
            }
            var query = new Dictionary<string, object>();
            if (props.Count > 0) query["$select"] = string.Join(",", props);
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/mailFolders/junkemail/messages", query);
            var messages = new List<Dictionary<string, object>>();
            while (!string.IsNullOrWhiteSpace(uri)) {
                var doc = await InvokeGraphApiAsync("GET", uri, headers);
                if (doc.RootElement.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array) {
                    foreach (var item in valueElement.EnumerateArray()) {
                        var native = ConvertJsonElementToNativeObject(item) as Dictionary<string, object>;
                        if (native != null) messages.Add(native);
                    }
                }
                uri = null;
                if (doc.RootElement.TryGetProperty("@odata.nextLink", out var next)) {
                    uri = next.GetString();
                }
            }
            if (skipIds != null || skipFrom != null || skipTo != null || skipSubjectContains != null || skipHasAttachment) {
                messages = FilterJunkMessages(messages, skipIds, skipFrom, skipTo, skipSubjectContains, skipHasAttachment);
            }

            if (skipAttachmentExtension != null && skipAttachmentExtension.Any()) {
                var result = new List<Dictionary<string, object>>();
                foreach (var msg in messages) {
                    if (!msg.TryGetValue("id", out var idObj) || idObj is not string id) continue;
                    if (msg.TryGetValue("hasAttachments", out var hasObj) && hasObj is bool hasAtt && hasAtt) {
                        var atts = await GetMailMessageAttachmentsAsync(
                            credential,
                            userPrincipalName,
                            id,
                            new[] { "name" });
                        if (atts.Any(att =>
                                skipAttachmentExtension.Contains(
                                    System.IO.Path.GetExtension(att.Name ?? string.Empty).TrimStart('.'),
                                    StringComparer.OrdinalIgnoreCase))) {
                            continue;
                        }
                    }
                    result.Add(msg);
                }
                messages = result;
            }
            return messages;
        }

        /// <summary>
        /// Moves a mail folder to another location.
        /// </summary>
        /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
        /// <param name="userPrincipalName">User principal name owning the mail folder.</param>
        /// <param name="folderId">Identifier of the folder to move.</param>
        /// <param name="destinationFolderId">Identifier of the new parent folder.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task MoveFolderAsync(
            GraphCredential credential,
            string userPrincipalName,
            string folderId,
            string destinationFolderId) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/mailFolders/{folderId}/move");
            var body = JsonSerializer.Serialize(new { destinationId = destinationFolderId });
            await InvokeGraphApiAsync("POST", uri, headers, body);
        }

        /// <summary>
        /// Renames a mail folder.
        /// </summary>
        /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
        /// <param name="userPrincipalName">User principal name owning the mail folder.</param>
        /// <param name="folderId">Identifier of the folder to rename.</param>
        /// <param name="newDisplayName">New display name for the folder.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RenameFolderAsync(
            GraphCredential credential,
            string userPrincipalName,
            string folderId,
            string newDisplayName) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/mailFolders/{folderId}");
            var body = JsonSerializer.Serialize(new { displayName = newDisplayName });
            await InvokeGraphApiAsync("PATCH", uri, headers, body);
        }

        /// <summary>
        /// Removes a mail folder.
        /// </summary>
        /// <param name="credential">Credential used to authenticate to Microsoft Graph.</param>
        /// <param name="userPrincipalName">User principal name owning the mail folder.</param>
        /// <param name="folderId">Identifier of the folder to remove.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RemoveFolderAsync(
            GraphCredential credential,
            string userPrincipalName,
            string folderId) {
            var headers = new Dictionary<string, string>();
            var token = await ConnectO365GraphAsync(credential, credential.DirectoryId, "https://graph.microsoft.com");
            headers["Authorization"] = token;
            var uri = JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{userPrincipalName}/mailFolders/{folderId}");
            await InvokeGraphApiAsync("DELETE", uri, headers);
        }
    }
}