using System.Net.Http.Headers;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// Helper class for sending messages via Microsoft Graph API.
/// </summary>
public class Graph : IDisposable {
    private readonly HttpClient _client;
    public string MessageJson = string.Empty;
    public GraphMessageContainer MessageContainer;
    public readonly Stopwatch Stopwatch;

    /// <summary>
    /// Value indicating whether the total size of the attachments is larger than 4MB.
    /// </summary>
    public bool IsLargerAttachment { get; set; }

    /// <summary>
    /// List of GraphAttachment objects created from the file paths in the Attachments property.
    /// </summary>
    public List<GraphAttachment> ConvertedAttachments { get; set; } = new List<GraphAttachment>();

    public List<GraphAttachmentPlaceHolder> AttachmentsPlaceHolders { get; set; } = new List<GraphAttachmentPlaceHolder>();

    /// <summary>
    /// Collection of attachments which can be file paths or GraphAttachment objects.
    /// </summary>
    public object[]? Attachments { get; set; }

    /// <summary>
    /// Gets or sets the sender. Can be a string (email) or a dictionary with Name and Email.
    ///
    /// Note: The display name ("Name") for the sender is controlled by Office 365 and may not reflect the value you provide here.
    /// Office 365 will use the mailbox's configured display name for the sender, regardless of what is set in the payload.
    /// The email address must be used for API calls and authentication.
    /// </summary>
    public object? From { get; set; }

    /// <summary>
    /// Gets or sets the email address to reply to.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the email addresses of the recipients.
    /// </summary>
    public object[]? To { get; set; }

    /// <summary>
    /// Gets or sets the email addresses of the CC recipients.
    /// </summary>
    public object[]? Cc { get; set; }

    /// <summary>
    /// Gets or sets the email addresses of the BCC recipients.
    /// </summary>
    public object[]? Bcc { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// HTML content of the email.
    /// </summary>
    public string HTML { get; set; }

    /// <summary>
    /// Content type of the email.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Value indicating whether the message should not be saved to the Sent Items folder.
    /// </summary>
    public bool DoNotSaveToSentItems { get; set; }

    /// <summary>
    /// Access token for the Graph API.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Application ID for the Graph API.
    /// </summary>
    private string ApplicationID { get; set; }

    /// <summary>
    /// Application key for the Graph API.
    /// </summary>
    private string ApplicationKey { get; set; }

    /// <summary>
    /// Tenant domain for the Graph API.
    /// </summary>
    private string TenantDomain { get; set; }

    /// <summary>
    /// Action to take when an error occurs based on the ErrorAction preference.
    /// </summary>
    public ActionPreference? ErrorAction { get; set; }

    /// <summary>
    /// Number of times to retry sending the message when an error occurs.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Delay in milliseconds between retry attempts.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Factor used to increase the delay for each subsequent retry. A value of
    /// 1 disables backoff.
    /// </summary>
    public double RetryDelayBackoff { get; set; } = 1.0;

    public bool AutoEmbedImages { get; set; } = false;

    /// <summary>
    /// Forces retries even when the encountered error is not classified as
    /// transient.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Size in bytes of the chunks used when uploading attachments. Defaults to
    /// 9MB.
    /// </summary>
    public int ChunkSize { get; set; } = 9000000;

    /// <summary>
    /// The type of token that was issued.
    /// </summary>
    public string TokenType { get; set; }

    /// <summary>
    /// The email address that the message was sent from.
    /// </summary>
    public string SentFrom => From == null ? string.Empty : Helpers.GetEmailAddress(From);

    /// <summary>
    /// A comma-separated list of email addresses that the message was sent to.
    /// </summary>
    public string SentTo {
        get {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(Helpers.UniqueAddresses(To, seen).Select(obj => Helpers.GetEmailAddress(obj)));
            }
            if (Cc != null) {
                addresses.AddRange(Helpers.UniqueAddresses(Cc, seen).Select(obj => Helpers.GetEmailAddress(obj)));
            }
            if (Bcc != null) {
                addresses.AddRange(Helpers.UniqueAddresses(Bcc, seen).Select(obj => Helpers.GetEmailAddress(obj)));
            }
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Request a read receipt for the message.
    /// </summary>
    public bool RequestReadReceipt { get; set; }

    /// <summary>
    /// Request a delivery receipt for the message.
    /// </summary>
    public bool RequestDeliveryReceipt { get; set; }

    public LogCollector LogCollector { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the Graph class.
    /// </summary>
    public Graph() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
        if (LogCollector == null) LogCollector = new();
    }

    /// <summary>
    /// Converts the <see cref="Attachments"/> collection into <see cref="GraphAttachment"/> instances.
    /// </summary>
    public void CreateAttachments() {
        ConvertedAttachments.Clear();
        if (Attachments != null && Attachments.Any()) {
            // Convert provided attachments into GraphAttachment objects
            foreach (var item in Attachments) {
                if (item is string path) {
                    if (!File.Exists(path)) {
                        LogCollector.LogWarning($"Send-EmailMessage - Attachment file not found: {path}");
                        LogCollector.LogWarning($"Send-EmailMessage - Possible issue: Path '{path}' is invalid. Verify the file exists and the path is correct.");
                    }
                    ConvertedAttachments.Add(GraphAttachment.FromFile(path));
                } else if (item is GraphAttachment ga) {
                    ConvertedAttachments.Add(ga);
                }
            }

            var totalSize = 0;
            foreach (var a in ConvertedAttachments) {
                if (string.IsNullOrEmpty(a.ContentBytes)) {
                    continue;
                }
                try {
                    totalSize += Convert.FromBase64String(a.ContentBytes).Length;
                } catch (FormatException ex) {
                    LogCollector.LogError($"Send-EmailMessage - Invalid base64 for attachment '{a.Name}': {ex.Message}");
                }
            }

            if (totalSize > 4_000_000) {
                // Create a draft message if the total size of the attachments is larger than 4MB
                IsLargerAttachment = true;
            } else {
                // Otherwise, include the attachments in the message
                IsLargerAttachment = false;
            }
        }
    }

    /// <summary>
    /// Builds the <see cref="GraphMessageContainer"/> object that represents the email.
    /// </summary>
    public void CreateMessage() {
        CreateAttachments();
        if (AutoEmbedImages) {
            var (html, paths) = HtmlUtils.ExtractLocalImagePaths(HTML);
            HTML = html;
            foreach (var p in paths) {
                var att = GraphAttachment.FromFile(p);
                att.IsInline = true;
                att.ContentId = Path.GetFileName(p);
                ConvertedAttachments.Add(att);
            }
        }
        // Note: The display name for the sender is controlled by Office 365 and may not reflect the value you provide here.
        // Office 365 will use the mailbox's configured display name for the sender, regardless of what is set in the payload.
        // Always use the email address for API calls and authentication.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        MessageContainer = new GraphMessageContainer {
            Message = new GraphMessage {
                From = ConvertToGraphEmailAddress(From),
                To = ConvertToGraphEmailAddressUnique(To, seen),
                Cc = ConvertToGraphEmailAddressUnique(Cc, seen),
                Bcc = ConvertToGraphEmailAddressUnique(Bcc, seen),
                ReplyTo = string.IsNullOrWhiteSpace(ReplyTo)
                    ? null
                    : new List<GraphEmailAddress> { ConvertToGraphEmailAddress(ReplyTo)! },
                Subject = Subject,
                Body = new GraphContent { Content = HTML, Type = ContentType },
                IsDeliveryReceiptRequested = RequestDeliveryReceipt,
                IsReadReceiptRequested = RequestReadReceipt
            },
            SaveToSentItems = !DoNotSaveToSentItems
        };
        if (ConvertedAttachments.Count > 0 && IsLargerAttachment == false) {
            MessageContainer.Message.Attachments = ConvertedAttachments;
        }

        var options = new JsonSerializerOptions() {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            //WriteIndented = true
        };
        MessageJson = JsonSerializer.Serialize(MessageContainer, options);
        //LoggingMessages.Logger.WriteVerbose(MessageJson);
    }

    /// <summary>
    /// Parses the provided credentials into client id, secret and tenant domain.
    /// </summary>
    /// <param name="Credentials">The credentials to parse.</param>
    public void Authenticate(ICredentials Credentials) {
        var networkCredential = Credentials as NetworkCredential;
        if (networkCredential != null) {
            var userSplit = networkCredential.UserName.Split('@');
            if (userSplit.Length != 2) {
                throw new ArgumentException(
                    "Credential.UserName must be in the format 'clientid@directoryid'",
                    nameof(Credentials));
            }

            ApplicationID = userSplit[0];
            ApplicationKey = networkCredential.Password;
            TenantDomain = userSplit[1];
        }
    }

    private GraphEmailAddress? ConvertToGraphEmailAddress(object? email) {
        if (email == null) {
            return null;
        }
        var address = Helpers.GetEmailAddress(email);
        return new GraphEmailAddress { Email = new GraphEmail { Address = address } };
    }

    private List<GraphEmailAddress>? ConvertToGraphEmailAddress(object[]? emails) {
        if (emails == null) {
            return null;
        }
        return emails.Select(email => new GraphEmailAddress { Email = new GraphEmail { Address = Helpers.GetEmailAddress(email) } }).ToList();
    }

    private List<GraphEmailAddress>? ConvertToGraphEmailAddressUnique(object[]? emails, HashSet<string> seen) {
        if (emails == null) {
            return null;
        }

        var list = new List<GraphEmailAddress>();
        foreach (var email in Helpers.UniqueAddresses(emails, seen)) {
            var address = Helpers.GetEmailAddress(email);
            list.Add(new GraphEmailAddress { Email = new GraphEmail { Address = address } });
        }
        return list.Count == 0 ? null : list;
    }

    /// <summary>
    /// Authenticates to Microsoft Graph using client credentials and obtains an access token.
    /// </summary>
    /// <returns>The result of the connection attempt.</returns>
    public async Task<SmtpResult> ConnectO365GraphAsync(CancellationToken cancellationToken = default) {
        string resource = "https://graph.microsoft.com";
        var body = new Dictionary<string, string> {
            { "grant_type", "client_credentials" },
            { "resource", resource },
            { "client_id", ApplicationID },
            { "client_secret", ApplicationKey }
        };

        //LoggingMessages.Logger.WriteVerbose($"Application ID: {ApplicationID}");
        //LoggingMessages.Logger.WriteVerbose($"Tenant Domain: {TenantDomain}");
        //LoggingMessages.Logger.WriteVerbose($"Application Key {ApplicationKey}");
        try {
            using var response = await _client.PostAsync($"https://login.microsoftonline.com/{TenantDomain}/oauth2/token", new FormUrlEncodedContent(body), cancellationToken);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                LogCollector.LogWarning($"Send-EmailMessage - Error during connection using Graph API: {content}");
                if (ErrorAction == ActionPreference.Stop) {
                    response.EnsureSuccessStatusCode();
                }
                return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, content, content);
            }

            var authorization = JsonSerializer.Deserialize<GraphAuthorization>(content);
            AccessToken = authorization.AccessToken;
            TokenType = authorization.TokenType;
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, "", "");
        } catch (TaskCanceledException ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Connection to Graph API cancelled: {ex.Message}");
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, string.Empty, ex.Message);
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Error during connection using Graph API: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// Sends the prepared message via the Graph API.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<SmtpResult> SendMessageAsync(CancellationToken cancellationToken = default) {
        // create message
        CreateMessage();
        LogCollector.LogVerbose("Send-EmailMessage - Sending email via Graph API");
        // Create the request URI outside the loop.
        var requestUri = "https://graph.microsoft.com/v1.0/users/" + MessageContainer.Message.From.Email.Address + "/sendMail";

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri) {
                    Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

                using var response = await _client.SendAsync(request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode) {
                    var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, response.StatusCode.ToString(), "");
                    await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken);
                    return okResult;
                }
                var error = JsonSerializer.Deserialize<GraphApiError>(content);
                var errorMessage = (error == null || error.Error == null || error.Error.InnerError == null)
                    ? $"Unknown error: {content}"
                    : $"Error code: {error.Error.Code}, message: {error.Error.Message}, request ID: {error.Error.InnerError.RequestId}, date: {error.Error.InnerError.Date}";
                throw new HttpRequestException(errorMessage);
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Sending via Graph API cancelled: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, string.Empty, ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }
                var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delayMilliseconds > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
                }
            } catch (Exception ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, "", ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }
                var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delayMilliseconds > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);

        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, "", lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return finalResult;
    }

    /// <summary>
    /// Sends a message by first creating a draft and then uploading attachments.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<SmtpResult> SendMessageDraftAsync(CancellationToken cancellationToken = default) {
        // Create the draft message using the new method
        var draftMessage = await CreateDraftMessageAsync(cancellationToken);

        // Upload attachments to the draft message
        await UploadAttachmentsAsync(draftMessage, cancellationToken);

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                return await SendDraftMessage(draftMessage, cancellationToken);
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Sending draft via Graph API cancelled: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, string.Empty, ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }
                var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delayMilliseconds > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
                }
            } catch (Exception ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, "", ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }
                var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delayMilliseconds > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);

        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, "", lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return finalResult;
    }

    /// <summary>
    /// Sends a previously created draft message.
    /// </summary>
    /// <param name="draftMessage">The draft message to send.</param>
    /// <returns>The result of the send operation.</returns>
    public async Task<SmtpResult> SendDraftMessage(GraphMessage draftMessage, CancellationToken cancellationToken = default) {
        // Send the draft message
        var sendRequestUri = $"https://graph.microsoft.com/v1.0/users/{MessageContainer.Message.From.Email.Address}/messages/{draftMessage.Id}/send";
        using var sendRequest = new HttpRequestMessage(HttpMethod.Post, sendRequestUri);

        // Add the authorization header
        sendRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for sending the draft message
        using var sendResponse = await _client.SendAsync(sendRequest, cancellationToken);

        // If the status code indicates success, return a successful result
        if (sendResponse.IsSuccessStatusCode) {
            var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, sendResponse.StatusCode.ToString(), "");
            await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken);
            return okResult;
        }

        // If the status code indicates an error, throw an exception with the content
        var sendContent = await sendResponse.Content.ReadAsStringAsync();
        var sendError = JsonSerializer.Deserialize<GraphApiError>(sendContent);
        var sendErrorMessage = (sendError == null || sendError.Error == null || sendError.Error.InnerError == null)
            ? $"Unknown error: {sendContent}"
            : $"Error code: {sendError.Error.Code}, message: {sendError.Error.Message}, request ID: {sendError.Error.InnerError.RequestId}, date: {sendError.Error.InnerError.Date}";
        var ex = new HttpRequestException(sendErrorMessage);
        var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, sendContent, ex.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
        throw ex;
    }

    /// <summary>
    /// Creates a draft message on the server and returns the resulting <see cref="GraphMessage"/>.
    /// </summary>
    /// <returns>The created draft message.</returns>
    public async Task<GraphMessage> CreateDraftMessageAsync(CancellationToken cancellationToken = default) {
        // Create the draft message
        CreateMessage();

        //var options = new JsonSerializerOptions() {
        //    WriteIndented = true
        //};

        //// Serialize only the GraphMessage to a JSON string, excluding the SaveToSentItems property
        //var messageJson = JsonSerializer.Serialize(MessageContainer.Message, options);

        var messageJson = CreateDraft();

        var draftRequestUri = $"https://graph.microsoft.com/v1.0/users/{MessageContainer.Message.From.Email.Address}/mailfolders/drafts/messages";
        var draftRequest = new HttpRequestMessage(HttpMethod.Post, draftRequestUri) {
            Content = new StringContent(messageJson, Encoding.UTF8, "application/json")
        };

        // Add the authorization header
        draftRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for creating the draft message
        using var draftResponse = await _client.SendAsync(draftRequest, cancellationToken);

        // Read the response content
        var draftContent = await draftResponse.Content.ReadAsStringAsync();

        if (!draftResponse.IsSuccessStatusCode) {
            var error = JsonSerializer.Deserialize<GraphApiError>(draftContent);
            var errorMessage = (error == null || error.Error == null)
                ? $"Unknown error: {draftContent}"
                : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
            throw new HttpRequestException(errorMessage);
        }

        // Deserialize the draft message
        var draftMessage = JsonSerializer.Deserialize<GraphMessage>(draftContent);

        if (draftMessage == null) {
            throw new InvalidOperationException("Failed to create draft message.");
        }

        return draftMessage;
    }

    /// <summary>
    /// Creates a draft message locally and returns its JSON representation.
    /// </summary>
    /// <returns>The JSON payload for the draft message.</returns>
    public string CreateDraftForMg() {
        // Create the draft message
        CreateMessage();
        var messageJson = CreateDraft();
        return messageJson;
    }

    /// <summary>
    /// Serializes the current message to JSON without saving it to the Sent Items folder.
    /// </summary>
    /// <returns>The JSON representation of the message.</returns>
    public string CreateDraft() {
        CreateMessage();

        var options = new JsonSerializerOptions() {
            WriteIndented = true
        };

        // Serialize only the GraphMessage to a JSON string, excluding the SaveToSentItems property
        var messageJson = JsonSerializer.Serialize(MessageContainer.Message, options);
        return messageJson;
    }


    /// <summary>
    /// Creates the metadata and content placeholders required for uploading a file attachment.
    /// </summary>
    /// <param name="attachmentPath">Path to the attachment file.</param>
    /// <returns>The placeholder representing the attachment.</returns>
    public async Task<GraphAttachmentPlaceHolder> CreateGraphAttachment(string attachmentPath, CancellationToken cancellationToken = default) {
        var fileName = Path.GetFileName(attachmentPath);
        var fileSize = new FileInfo(attachmentPath).Length;

        var attachmentItem = new GraphAttachmentItem("file", fileName, fileSize);

        var attachmentItemWrapper = new GraphAttachmentItemWrapper(attachmentItem);
        var attachmentItemJson = JsonSerializer.Serialize(attachmentItemWrapper);

        var content = await PrepareByteArrayContentForUpload(attachmentPath, ChunkSize, cancellationToken);

        return new GraphAttachmentPlaceHolder() {
            Json = attachmentItemJson,
            Content = content,
            FileSize = fileSize,
            FileName = fileName
        };
    }

    /// <summary>
    /// Creates an upload session for a large attachment.
    /// </summary>
    /// <param name="draftMessage">The draft message the attachment belongs to.</param>
    /// <param name="attachmentItemJson">The serialized attachment item.</param>
    /// <returns>The upload session URL.</returns>
    public async Task<string> CreateUploadSession(GraphMessage draftMessage, string attachmentItemJson, CancellationToken cancellationToken = default) {
        var uploadSessionUrl = $"https://graph.microsoft.com/v1.0/users('{SentFrom}')/messages/{draftMessage.Id}/attachments/createUploadSession";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        using var uploadSessionResponse = await client.PostAsync(uploadSessionUrl, new StringContent(attachmentItemJson, Encoding.UTF8, "application/json"), cancellationToken);
        var uploadSessionContent = await uploadSessionResponse.Content.ReadAsStringAsync();

        // {"error":{"code":"InvalidAuthenticationToken","message":"Access token is empty.","innerError":{"date":"2024-06-15T09:51:54","request-id":"4a43e743-e897-4758-8d7d-21858c198e1d","client-request-id":"4a43e743-e897-4758-8d7d-21858c198e1d"}}}
        //Console.WriteLine(uploadSessionContent);
        var uploadSessionResult = JsonSerializer.Deserialize<GraphUploadSessionResult>(uploadSessionContent);

        var uploadUrl = uploadSessionResult?.UploadUrl ?? throw new InvalidOperationException("Upload URL not found.");
        return uploadUrl;
    }

    /// <summary>
    /// 9000000 = 9MB
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="chunkSize"></param>
    /// <returns></returns>
    private async Task<List<ByteArrayContent>> PrepareByteArrayContentForUpload(string filePath, int chunkSize = 9000000, CancellationToken cancellationToken = default) {
        var fileContents = new List<ByteArrayContent>();
        var fileSize = new FileInfo(filePath).Length;

        using var fileStream = new FileStream(filePath, FileMode.Open);
        var buffer = new byte[chunkSize];
        int bytesRead;
        long offset = 0;
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
            var contentRange = $"bytes {offset}-{offset + bytesRead - 1}/{fileSize}";
            var byteArrayContent = new ByteArrayContent(buffer, 0, bytesRead);
            byteArrayContent.Headers.Add("Content-Range", contentRange);
            fileContents.Add(byteArrayContent);
            offset += bytesRead;
        }

        return fileContents;
    }

    /// <summary>
    /// Uploads all attachments for the specified draft message.
    /// </summary>
    /// <param name="draftMessage">The draft message to attach the files to.</param>
    public async Task UploadAttachmentsAsync(GraphMessage draftMessage, CancellationToken cancellationToken = default) {
        if (Attachments != null && Attachments.Length > 0) {
            foreach (var attachmentPath in Attachments) {
                if (attachmentPath is string path) {
                    var attachmentItemJson = await CreateGraphAttachment(path, cancellationToken);
                    var uploadUrl = await CreateUploadSession(draftMessage, attachmentItemJson.Json, cancellationToken);
                    await SendFileChunks(uploadUrl, attachmentItemJson.Content, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Prepares attachments for upload by creating placeholders.
    /// </summary>
    public async Task PrepareAttachments(CancellationToken cancellationToken = default) {
        if (Attachments != null && Attachments.Length > 0) {
            foreach (var attachmentPath in Attachments) {
                if (attachmentPath is string path) {
                    var attachmentItemJson = await CreateGraphAttachment(path, cancellationToken);
                    AttachmentsPlaceHolders.Add(attachmentItemJson);
                }
            }
        }
    }

    /// <summary>
    /// Uploads all chunks of a file to the provided upload session URL.
    /// </summary>
    /// <param name="uploadUrl">The upload session URL.</param>
    /// <param name="fileChunks">The file chunks to upload.</param>
    public async Task SendFileChunks(string uploadUrl, List<ByteArrayContent> fileChunks, CancellationToken cancellationToken = default) {
        var tasks = new List<Task>();
        foreach (var chunk in fileChunks) {
            tasks.Add(SendFile(uploadUrl, chunk, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Uploads a single file chunk to the Graph API.
    /// </summary>
    /// <param name="uploadUrl">The upload session URL.</param>
    /// <param name="byteArrayContent">The chunk to send.</param>
    public async Task SendFile(string uploadUrl, ByteArrayContent byteArrayContent, CancellationToken cancellationToken = default) {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, uploadUrl) {
            Content = byteArrayContent
        };
        requestMessage.Headers.Add("AnchorMailbox", SentFrom); // This is correctly added to HttpRequestMessage
        using var client = new HttpClient();
        var uploadChunkResponse = await client.SendAsync(requestMessage, cancellationToken);
        if (!uploadChunkResponse.IsSuccessStatusCode) {
            // Handle upload error
            LogCollector.LogWarning(uploadChunkResponse.ToString());
            return;
        }
    }

    /// <summary>
    /// Releases resources used by the Graph client.
    /// </summary>
    public void Dispose() {
        _client.Dispose();
    }
}