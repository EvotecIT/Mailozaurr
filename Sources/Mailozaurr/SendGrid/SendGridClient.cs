using System.Diagnostics;

namespace Mailozaurr;

/// <summary>
/// A client for sending emails using the SendGrid API.
/// </summary>
public class SendGridClient {
    /// <summary>
    /// Gets the JSON representation of the message to be sent.
    /// </summary>
    private string MessageJson { get; set; }

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
    public object From { get; set; }

    /// <summary>
    /// Gets or sets the list of primary recipients of the email.
    /// </summary>
    public List<object> To { get; set; }

    /// <summary>
    /// Gets or sets the list of carbon copy (CC) recipients of the email.
    /// </summary>
    public List<object> Cc { get; set; }

    /// <summary>
    /// Gets or sets the list of blind carbon copy (BCC) recipients of the email.
    /// </summary>
    public List<object> Bcc { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address for the email.
    /// </summary>
    public object? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the paths of the files to be attached to the email.
    /// </summary>
    public string[]? Attachment { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the plain text content of the email.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the HTML content of the email.
    /// </summary>
    public string Html { get; set; }

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
    public ICredentials Credentials { get; set; }

    /// <summary>
    /// Gets or sets the action to take when an error occurs.
    /// </summary>
    public ActionPreference? ErrorAction { get; set; }

    /// <summary>
    /// Gets a string containing the email addresses of all recipients of the email.
    /// </summary>
    public string SentTo {
        get {
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(To.Select(ConvertToEmailObject).Where(x => x != null).Select(x => x.Email));
            }
            if (Cc != null) {
                addresses.AddRange(Cc.Select(ConvertToEmailObject).Where(x => x != null).Select(x => x.Email));
            }
            if (Bcc != null) {
                addresses.AddRange(Bcc.Select(ConvertToEmailObject).Where(x => x != null).Select(x => x.Email));
            }
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Gets the email address of the sender of the email.
    /// </summary>
    public string SentFrom => From.ToString();

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
        if (emailAddress == null || string.IsNullOrEmpty(emailAddress.ToString())) {
            return null;
        } else if (emailAddress is string emailString) {
            return new SendGridEmailAddress { Email = emailString };
        } else if (emailAddress is IDictionary<string, object> emailDict) {
            return new SendGridEmailAddress { Email = emailDict["Email"] as string, Name = emailDict["Name"] as string };
        } else {
            throw new ArgumentException($"email object type {emailAddress.GetType().Name} requires addition");
        }
    }

    /// <summary>
    /// Creates a SendGridMessage object from the properties of this SendGridClient.
    /// </summary>
    public void CreateMessage() {

        var attachments = Attachment?.Select(path => new SendGridAttachment(path)).ToList();

        var personalizations = new List<SendGridPersonalization> {
                new SendGridPersonalization {
                    To = To?.Where(t => t != null).Select(ConvertToEmailObject).ToList(),
                    Cc = Cc?.Where(c => c != null).Select(ConvertToEmailObject).ToList(),
                    Bcc = Bcc?.Where(b => b != null).Select(ConvertToEmailObject).ToList()
                }
            }.Where(p => p.To != null || p.Cc != null || p.Bcc != null).ToList();

        var content = new List<SendGridContent> {
                new SendGridContent { Type = "text/plain", Value = Text },
                new SendGridContent { Type = "text/html", Value = Html }
            }.Where(c => c.Value != null).ToList();


        var message = new SendGridMessage {
            Personalizations = personalizations,
            From = ConvertToEmailObject(From),
            Subject = Subject,
            Content = content,
            ReplyTo = ConvertToEmailObject(ReplyTo),
            Attachments = attachments
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
    public async Task<SmtpResult> SendEmailAsync() {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send") {
            Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
        };

        string apiKey;
        try {
            var networkCredential = Credentials as NetworkCredential;
            apiKey = networkCredential.Password;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using SendGrid: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, "", ex.Message);
        }

        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        try {
            var response = await _client.SendAsync(request);
            LoggingMessages.Logger.WriteVerbose($"Send-EmailMessage - Sent email to {SentTo} using SendGrid");
            return new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, response.EnsureSuccessStatusCode().ToString());
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using SendGrid: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, "", ex.Message);
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the SendGridClient and optionally releases the managed resources.
    /// </summary>
    public void Dispose() {
        _client.Dispose();
    }
}
