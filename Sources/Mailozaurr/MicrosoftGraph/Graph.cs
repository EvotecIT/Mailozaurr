using System.Diagnostics;
using System.Net.Http.Headers;

namespace Mailozaurr;

public class Graph {
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

    /// <summary>
    /// Array of file paths to the attachments.
    /// </summary>
    public string[]? Attachments { get; set; }

    /// <summary>
    /// Gets or sets the email address of the sender.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the email address to reply to.
    /// </summary>
    public string ReplyTo { get; set; }

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
    /// The type of token that was issued.
    /// </summary>
    public string TokenType { get; set; }

    /// <summary>
    /// The email address that the message was sent from.
    /// </summary>
    public string SentFrom => From;

    /// <summary>
    /// A comma-separated list of email addresses that the message was sent to.
    /// </summary>
    public string SentTo {
        get {
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(To.Select(email => email.ToString()));
            }
            if (Cc != null) {
                addresses.AddRange(Cc.Select(email => email.ToString()));
            }
            if (Bcc != null) {
                addresses.AddRange(Bcc.Select(email => email.ToString()));
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

    /// <summary>
    /// Initializes a new instance of the Graph class.
    /// </summary>
    public Graph() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
    }

    public void CreateAttachments() {
        if (Attachments != null && Attachments.Any()) {
            // Convert file paths to GraphAttachment objects and add them to the Attachments list
            ConvertedAttachments = Attachments.Select(path => GraphAttachment.FromFile(path)).ToList();

            if (ConvertedAttachments.Sum(a => a.ContentBytes.Length) > 4000000) {
                // Create a draft message if the total size of the attachments is larger than 4MB
                IsLargerAttachment = true;
            } else {
                // Otherwise, include the attachments in the message
                IsLargerAttachment = false;
            }
        }
    }

    public void CreateMessage() {
        // Convert the GraphMessage to a JSON string.
        MessageContainer = new GraphMessageContainer {
            Message = new GraphMessage {
                From = ConvertToGraphEmailAddress(From),
                To = ConvertToGraphEmailAddress(To),
                Cc = ConvertToGraphEmailAddress(Cc),
                Bcc = ConvertToGraphEmailAddress(Bcc),
                ReplyTo = string.IsNullOrEmpty(ReplyTo) ? null : ConvertToGraphEmailAddress([ReplyTo]),
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

        // Convert the GraphMessage to a JSON string.
        var options = new JsonSerializerOptions() {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            //WriteIndented = true
        };
        MessageJson = JsonSerializer.Serialize(MessageContainer, options);
        //LoggingMessages.Logger.WriteVerbose(MessageJson);
    }

    public void Authenticate(ICredentials Credentials) {
        var networkCredential = Credentials as NetworkCredential;
        if (networkCredential != null) {
            var userSplit = networkCredential.UserName.Split('@');
            ApplicationID = userSplit[0];
            ApplicationKey = networkCredential.Password;
            TenantDomain = userSplit[1];
        }
    }

    private GraphEmailAddress? ConvertToGraphEmailAddress(object? email) {
        if (email == null) {
            return null;
        }

        return new GraphEmailAddress { Email = new GraphEmail { Address = email.ToString() } };
    }

    private List<GraphEmailAddress>? ConvertToGraphEmailAddress(object[]? emails) {
        if (emails == null) {
            return null;
        }

        return emails.Select(email => new GraphEmailAddress { Email = new GraphEmail { Address = email.ToString() } }).ToList();
    }

    public async Task<SmtpResult> ConnectO365GraphAsync() {
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
        HttpResponseMessage response;
        try {
            response = await _client.PostAsync($"https://login.microsoftonline.com/{TenantDomain}/oauth2/token", new FormUrlEncodedContent(body));
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during connection using Graph API: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, "", ex.Message);

        }

        var statusCode = response.EnsureSuccessStatusCode();
        //LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Got status code: {statusCode}");

        try {
            var content = await response.Content.ReadAsStringAsync();
            var authorization = JsonSerializer.Deserialize<Authorization>(content);
            //LoggingMessages.Logger.WriteVerbose($"AccessToken {authorization.AccessToken}");
            //LoggingMessages.Logger.WriteVerbose($"TokenType {authorization.TokenType}");
            AccessToken = authorization.AccessToken;
            TokenType = authorization.TokenType;
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, "", "");
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during connection using Graph API: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    public async Task<SmtpResult> SendMessageAsync() {
        // create message
        CreateMessage();

        LoggingMessages.Logger.WriteVerbose("Send-EmailMessage - Sending email via Graph API");
        // Create the HTTP request.
        var requestUri = "https://graph.microsoft.com/v1.0/users/" + MessageContainer.Message.From.Email.Address + "/sendMail";
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri) {
            Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
        };
        //LoggingMessages.Logger.WriteVerbose($"Url: {requestUri}");
        //LoggingMessages.Logger.WriteVerbose($"AccessToken Before Sent: {TokenType} {AccessToken}");

        // Add the authorization header.
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        try {
            // Send the HTTP request.
            var response = await _client.SendAsync(request);
            // Read the response content.
            var content = await response.Content.ReadAsStringAsync();
            // If the status code indicates success, return a successful result.
            if (response.IsSuccessStatusCode) {
                return new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, response.StatusCode.ToString(), "");
            }
            // If the status code indicates an error, throw an exception with the content.
            var error = JsonSerializer.Deserialize<GraphApiError>(content);
            var errorMessage = $"Error code: {error.Error.Code}, message: {error.Error.Message}, request ID: {error.Error.InnerError.RequestId}, date: {error.Error.InnerError.Date}";
            throw new HttpRequestException(errorMessage);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    public async Task<SmtpResult> SendMessageDraftAsync() {
        // Create the draft message using the new method
        var draftMessage = await CreateDraftMessageAsync();

        if (draftMessage != null && Attachments?.Length > 0) {
            // Upload attachments to the draft message
            await UploadAttachmentsAsync(draftMessage);
        }

        // Send the draft message
        var sendRequestUri = $"https://graph.microsoft.com/v1.0/users/{MessageContainer.Message.From.Email.Address}/messages/{draftMessage.Id}/send";
        var sendRequest = new HttpRequestMessage(HttpMethod.Post, sendRequestUri);

        // Add the authorization header
        sendRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for sending the draft message
        var sendResponse = await _client.SendAsync(sendRequest);

        // If the status code indicates success, return a successful result
        if (sendResponse.IsSuccessStatusCode) {
            return new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, Stopwatch.Elapsed, sendResponse.StatusCode.ToString(), "");
        }

        // If the status code indicates an error, throw an exception with the content
        var sendContent = await sendResponse.Content.ReadAsStringAsync();
        var sendError = JsonSerializer.Deserialize<GraphApiError>(sendContent);
        var sendErrorMessage = $"Error code: {sendError.Error.Code}, message: {sendError.Error.Message}, request ID: {sendError.Error.InnerError.RequestId}, date: {sendError.Error.InnerError.Date}";
        throw new HttpRequestException(sendErrorMessage);
    }

    public async Task<GraphMessage> CreateDraftMessageAsync() {
        // Create the draft message
        CreateMessage();

        var draftRequestUri = $"https://graph.microsoft.com/v1.0/users/{MessageContainer.Message.From.Email.Address}/mailfolders/drafts/messages";

        var options = new JsonSerializerOptions() {
            WriteIndented = true
        };

        // Serialize only the GraphMessage to a JSON string, excluding the SaveToSentItems property
        var messageJson = JsonSerializer.Serialize(MessageContainer.Message, options);

        var draftRequest = new HttpRequestMessage(HttpMethod.Post, draftRequestUri) {
            Content = new StringContent(messageJson, Encoding.UTF8, "application/json")
        };

        // Add the authorization header
        draftRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for creating the draft message
        var draftResponse = await _client.SendAsync(draftRequest);

        // Read the response content
        var draftContent = await draftResponse.Content.ReadAsStringAsync();

        if (!draftResponse.IsSuccessStatusCode) {
            var error = JsonSerializer.Deserialize<GraphApiError>(draftContent);
            var errorMessage = error != null ? $"Error code: {error.Error.Code}, message: {error.Error.Message}" : "Unknown error";
            throw new HttpRequestException(errorMessage);
        }

        // Deserialize the draft message
        var draftMessage = JsonSerializer.Deserialize<GraphMessage>(draftContent);

        if (draftMessage == null) {
            throw new InvalidOperationException("Failed to create draft message.");
        }

        return draftMessage;
    }


    public async Task UploadAttachmentsAsync(GraphMessage draftMessage) {
        foreach (var attachmentPath in Attachments) {
            var fileName = Path.GetFileName(attachmentPath);
            var fileSize = new FileInfo(attachmentPath).Length;

            // Create an upload session
            var uploadSessionUrl = $"https://graph.microsoft.com/v1.0/users('{MessageContainer.Message.From.Email.Address}')/messages/{draftMessage.Id}/attachments/createUploadSession";

            var attachmentItem = new AttachmentItem("file", fileName, fileSize);

            var attachmentItemWrapper = new AttachmentItemWrapper(attachmentItem);
            var attachmentItemJson = JsonSerializer.Serialize(attachmentItemWrapper);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

            var uploadSessionResponse = await _client.PostAsync(uploadSessionUrl, new StringContent(attachmentItemJson, Encoding.UTF8, "application/json"));

            var uploadSessionContent = await uploadSessionResponse.Content.ReadAsStringAsync();

            // {"error":{"code":"InvalidAuthenticationToken","message":"Access token is empty.","innerError":{"date":"2024-06-15T09:51:54","request-id":"4a43e743-e897-4758-8d7d-21858c198e1d","client-request-id":"4a43e743-e897-4758-8d7d-21858c198e1d"}}}
            //Console.WriteLine(uploadSessionContent);
            var uploadSessionResult = JsonSerializer.Deserialize<UploadSessionResult>(uploadSessionContent);

            var uploadUrl = uploadSessionResult?.UploadUrl ?? throw new InvalidOperationException("Upload URL not found.");

            // Upload the file in chunks
            const int chunkSize = 9000000; // 9 MB
            using var fileStream = new FileStream(attachmentPath, FileMode.Open);
            var buffer = new byte[chunkSize];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                var contentRange = $"bytes 0-{bytesRead - 1}/{fileSize}";
                var byteArrayContent = new ByteArrayContent(buffer, 0, bytesRead);
                byteArrayContent.Headers.Add("Content-Range", contentRange); // Add Content-Range header to the HttpContent

                Console.WriteLine(uploadUrl);

                var requestMessage = new HttpRequestMessage(HttpMethod.Put, uploadUrl) {
                    Content = byteArrayContent
                };
                requestMessage.Headers.Add("AnchorMailbox", SentFrom); // This is correctly added to HttpRequestMessage
                _client.DefaultRequestHeaders.Authorization = null;
                var uploadChunkResponse = await _client.SendAsync(requestMessage);
                if (!uploadChunkResponse.IsSuccessStatusCode) {
                    // Handle upload error
                    Console.WriteLine(uploadChunkResponse);
                    break;
                }
            }
        }
    }

}

public class AttachmentItemWrapper {
    [JsonPropertyName("AttachmentItem")]
    public AttachmentItem AttachmentItem { get; set; }

    public AttachmentItemWrapper(AttachmentItem attachmentItem) {
        AttachmentItem = attachmentItem;
    }
}

public class UploadSessionResult {
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; }
}

public class AttachmentItem {
    [JsonPropertyName("attachmentType")]
    public string AttachmentType { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    public AttachmentItem(string attachmentType, string name, long size) {
        AttachmentType = attachmentType;
        Name = name;
        Size = size;
    }
}


public class GraphMessageContainer {
    [JsonPropertyName("message")]
    public GraphMessage Message { get; set; }
    [JsonPropertyName("saveToSentItems")]
    public bool SaveToSentItems { get; set; }
}

public class GraphMessage {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("from")]
    public GraphEmailAddress From { get; set; }

    [JsonPropertyName("toRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? To { get; set; }

    [JsonPropertyName("ccRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? Cc { get; set; }

    [JsonPropertyName("bccRecipients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? Bcc { get; set; }

    [JsonPropertyName("replyTo")]
    public List<GraphEmailAddress>? ReplyTo { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; }

    [JsonPropertyName("body")]
    public GraphContent Body { get; set; }

    [JsonPropertyName("importance")]
    public string Importance { get; set; }

    [JsonPropertyName("isReadReceiptRequested")]
    public bool IsReadReceiptRequested { get; set; }

    [JsonPropertyName("isDeliveryReceiptRequested")]
    public bool IsDeliveryReceiptRequested { get; set; }

    [JsonPropertyName("attachments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphAttachment>? Attachments { get; set; }

}

public class GraphEmailAddress {
    [JsonPropertyName("emailAddress")]
    public GraphEmail Email { get; set; }
}

public class GraphEmail {
    [JsonPropertyName("address")]
    public string Address { get; set; }
}

public class GraphContent {
    [JsonPropertyName("contentType")]
    public string Type { get; set; } = "Text";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class Authorization {
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
}

public class GraphApiError {
    [JsonPropertyName("error")]
    public GraphApiErrorDetail Error { get; set; }
}

public class GraphApiErrorDetail {
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("innerError")]
    public GraphApiInnerError InnerError { get; set; }
}

public class GraphApiInnerError {
    [JsonPropertyName("request-id")]
    public string RequestId { get; set; }

    [JsonPropertyName("client-request-id")]
    public string ClientRequestId { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}

public class GraphAttachment {
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "#microsoft.graph.fileAttachment";

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("contentBytes")]
    public string ContentBytes { get; set; }

    public static GraphAttachment FromFile(string filePath) {
        var fileInfo = new FileInfo(filePath);
        var fileBytes = File.ReadAllBytes(filePath);
        var fileContentBase64 = Convert.ToBase64String(fileBytes);

        return new GraphAttachment {
            Name = fileInfo.Name,
            ContentBytes = fileContentBase64
        };
    }
}
