using System.Threading;

namespace Mailozaurr;

/// <summary>
/// A client for sending emails using the SendGrid API.
/// </summary>
/// <remarks>
/// Only key functionality required by the module is implemented;
/// it is not intended as a full wrapper of the SendGrid SDK.
/// </remarks>
public class SendGridClient {
    /// <summary>
    /// Gets the JSON representation of the message to be sent.
    /// </summary>
    private string MessageJson { get; set; } = string.Empty;

    /// <summary>
    /// The HttpClient used to send HTTP requests.
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// Stopwatch to measure the time taken to send an email.
    /// </summary>
    public readonly Stopwatch Stopwatch;

    /// <summary>
    /// Gets or sets the sender of the email.
    /// </summary>
    public object? From { get; set; }

    /// <summary>
    /// Gets or sets the list of primary recipients of the email.
    /// </summary>
    public List<object>? To { get; set; }

    /// <summary>
    /// Gets or sets the list of carbon copy (CC) recipients of the email.
    /// </summary>
    public List<object>? Cc { get; set; }

    /// <summary>
    /// Gets or sets the list of blind carbon copy (BCC) recipients of the email.
    /// </summary>
    public List<object>? Bcc { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address for the email.
    /// </summary>
    public object? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the paths of the files to be attached to the email.
    /// </summary>
    public object[]? Attachment { get; set; }

    /// <summary>Custom headers to include with the message.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the plain text content of the email.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTML content of the email.
    /// </summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority of the email.
    /// </summary>
    public MessagePriority Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to send separate emails to each recipient.
    /// </summary>
    public bool SeparateTo { get; set; }

    /// <summary>
    /// Gets or sets the credentials used for authentication with the SendGrid API.
    /// </summary>
    public ICredentials? Credentials { get; set; }

    /// <summary>
    /// Gets or sets the action to take when an error occurs.
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

    /// <summary>
    /// If set to <c>true</c>, retries will occur even on non-transient
    /// failures. Otherwise only transient errors trigger retries.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    /// <summary>Webhook invoked after sending.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Gets a string containing the email addresses of all recipients of the email.
    /// </summary>
    public string SentTo {
        get {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(To.Select(ConvertToEmailObject).Where(x => x != null && seen.Add(x.Email)).Select(x => x!.Email));
            }
            if (Cc != null) {
                addresses.AddRange(Cc.Select(ConvertToEmailObject).Where(x => x != null && seen.Add(x.Email)).Select(x => x!.Email));
            }
            if (Bcc != null) {
                addresses.AddRange(Bcc.Select(ConvertToEmailObject).Where(x => x != null && seen.Add(x.Email)).Select(x => x!.Email));
            }
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Gets the email address of the sender of the email.
    /// </summary>
    public string SentFrom => From != null ? Helpers.GetEmailAddress(From) : string.Empty;

    /// <summary>
    /// Gets or sets the log collector for this client.
    /// </summary>
    public LogCollector LogCollector { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the SendGridClient class.
    /// </summary>
    public SendGridClient() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
    }

    /// <summary>
    /// Converts the provided object to a SendGridEmailAddress object.
    /// </summary>
    /// <param name="emailAddress">The object to convert.</param>
    /// <returns>A SendGridEmailAddress object, or null if the provided object is null or an empty string.</returns>
    private SendGridEmailAddress? ConvertToEmailObject(object? emailAddress) {
        if (emailAddress == null || string.IsNullOrWhiteSpace(emailAddress.ToString())) {
            return null;
        } else if (emailAddress is string emailString) {
            return new SendGridEmailAddress { Email = emailString };
        } else if (emailAddress is IDictionary<string, object> emailDict) {
            if (!emailDict.ContainsKey("Email")) {
                throw new ArgumentException("Dictionary is missing required key 'Email'.", nameof(emailAddress));
            }

            var emailValue = emailDict["Email"] as string;
            if (string.IsNullOrWhiteSpace(emailValue)) {
                return null;
            }

            var nameValue = emailDict.ContainsKey("Name") ? emailDict["Name"] as string : null;
            return new SendGridEmailAddress { Email = emailValue!, Name = nameValue };
        } else {
            throw new ArgumentException($"email object type {emailAddress.GetType().Name} requires addition");
        }
    }

    /// <summary>
    /// Converts an attachment object to a <see cref="SendGridAttachment"/>.
    /// </summary>
    /// <param name="attachment">The attachment object to convert.</param>
    /// <returns>The <see cref="SendGridAttachment"/> instance or <c>null</c> if the input is <c>null</c>.</returns>
    private static SendGridAttachment? ConvertToAttachment(object? attachment) {
        if (attachment == null) {
            return null;
        }

        return attachment switch {
            string path => new SendGridAttachment(path),
            SendGridAttachment sg => sg,
            _ => throw new ArgumentException($"attachment object type {attachment.GetType().Name} requires addition")
        };
    }

    /// <summary>
    /// Converts a collection of attachment objects to a list of <see cref="SendGridAttachment"/>.
    /// </summary>
    /// <param name="attachments">Attachments to convert.</param>
    /// <returns>List of converted attachments.</returns>
    private static List<SendGridAttachment> ConvertAttachments(IEnumerable<object>? attachments) {
        var result = new List<SendGridAttachment>();
        if (attachments == null) {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in attachments) {
            var converted = ConvertToAttachment(item);
            if (converted != null) {
                if (item is string path && !seen.Add(path)) {
                    continue;
                }
                result.Add(converted);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a SendGridMessage object from the properties of this SendGridClient.
    /// </summary>
    public void CreateMessage() {

        var attachments = ConvertAttachments(Attachment);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var personalizations = new List<SendGridPersonalization>
            {
                new SendGridPersonalization
                {
                    To = To?.Where(t => t != null)
                        .Select(ConvertToEmailObject)
                        .Where(x => x != null && seen.Add(x.Email))
                        .Select(x => x!)
                        .ToList(),
                    Cc = Cc?.Where(c => c != null)
                        .Select(ConvertToEmailObject)
                        .Where(x => x != null && seen.Add(x.Email))
                        .Select(x => x!)
                        .ToList(),
                    Bcc = Bcc?.Where(b => b != null)
                        .Select(ConvertToEmailObject)
                        .Where(x => x != null && seen.Add(x.Email))
                        .Select(x => x!)
                        .ToList()
                }
            }
            .Where(p => p.To != null || p.Cc != null || p.Bcc != null)
            .ToList();

        var content = new List<SendGridContent> {
                new SendGridContent { Type = "text/plain", Value = Text },
                new SendGridContent { Type = "text/html", Value = Html }
            }.Where(c => c.Value != null).ToList();


        var fromAddress = ConvertToEmailObject(From) ?? throw new InvalidOperationException("From address is required.");

        var message = new SendGridMessage {
            Personalizations = personalizations,
            From = fromAddress,
            Subject = Subject,
            Content = content,
            ReplyTo = ConvertToEmailObject(ReplyTo),
            Attachments = attachments,
            Headers = Headers
        };

        var options = new JsonSerializerOptions() {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
        MessageJson = JsonSerializer.Serialize(message, options);
        //Console.WriteLine(MessageJson);
    }

    /// <summary>
    /// Sends an email asynchronously using the SendGrid API.
    /// </summary>
    /// <returns>A Task that represents the asynchronous operation. The task result contains the result of the email sending operation.</returns>
    public Task<SmtpResult> SendEmailAsync() => SendEmailAsync(CancellationToken.None);

    /// <summary>
    /// Sends an email asynchronously using the SendGrid API.
    /// </summary>
    /// <returns>A Task that represents the asynchronous operation. The task result contains the result of the email sending operation.</returns>
    public async Task<SmtpResult> SendEmailAsync(CancellationToken cancellationToken) {
        string apiKey;
        if (Credentials is NetworkCredential networkCredential) {
            apiKey = networkCredential.Password;
        } else {
            const string message = "Credentials must be NetworkCredential";
            LogCollector.LogWarning($"Send-EmailMessage - Error during sending using SendGrid: {message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidCastException(message);
            }
            var credFail = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, string.Empty, message);
            await Helpers.PostWebhookAsync(WebhookUrl, credFail, cancellationToken);
            return credFail;
        }

        int attempts = 0;
        Exception? lastException = null;
        string? lastContent = null;
        do {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send") {
                    Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                using var response = await _client.SendAsync(request, cancellationToken);
#if NET5_0_OR_GREATER
                lastContent = await response.Content.ReadAsStringAsync(cancellationToken);
#else
                lastContent = await response.Content.ReadAsStringAsync();
#endif
                LogCollector.LogVerbose($"Send-EmailMessage - Sent email to {SentTo} using SendGrid");

                if (response.IsSuccessStatusCode) {
                    var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, response.StatusCode.ToString());
                    await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken);
                    return okResult;
                }

                var message = $"Status code {response.StatusCode}: {lastContent}";
                lastException = new HttpRequestException(message);
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using SendGrid: {message}");
            } catch (HttpRequestException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - HTTP error during sending using SendGrid: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop && lastException != null) {
                        throw lastException;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, lastException?.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }

                var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delayMilliseconds > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
                }
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Request canceled: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop && lastException != null) {
                        throw lastException;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, lastException?.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }
                var delayMs = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delayMs > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);

        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return finalResult;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the SendGridClient and optionally releases the managed resources.
    /// </summary>
    public void Dispose() {
        _client.Dispose();
    }
}
