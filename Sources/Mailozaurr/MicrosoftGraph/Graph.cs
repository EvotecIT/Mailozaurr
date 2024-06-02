using System.Diagnostics;

namespace Mailozaurr;

public class Graph {
    private readonly HttpClient _client;
    public string MessageJson = string.Empty;
    public GraphMessageContainer MessageContainer;
    public readonly Stopwatch Stopwatch;

    public string From { get; set; }
    public string ReplyTo { get; set; }
    public object[]? To { get; set; }
    public object[]? Cc { get; set; }
    public object[]? Bcc { get; set; }
    public string Subject { get; set; }
    public string HTML { get; set; }
    public string ContentType { get; set; }
    public bool DoNotSaveToSentItems { get; set; }

    public string AccessToken { get; set; }

    private string ApplicationID { get; set; }
    private string ApplicationKey { get; set; }
    private string TenantDomain { get; set; }

    public ActionPreference? ErrorAction { get; set; }

    public string TokenType { get; set; }

    public string SentFrom => From;

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

    public bool RequestReadReceipt { get; set; }
    public bool RequestDeliveryReceipt { get; set; }


    public Graph() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
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

        LoggingMessages.Logger.WriteVerbose($"Application ID: {ApplicationID}");
        LoggingMessages.Logger.WriteVerbose($"Tenant Domain: {TenantDomain}");
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
}

public class GraphMessageContainer {
    [JsonPropertyName("message")]
    public GraphMessage Message { get; set; }
    [JsonPropertyName("saveToSentItems")]
    public bool SaveToSentItems { get; set; }
}

public class GraphMessage {
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

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}
