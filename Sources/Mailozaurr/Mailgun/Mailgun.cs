using System.Net.Http.Headers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Mailozaurr;

/// <summary>
/// Simple client for sending emails using the Mailgun API.
/// </summary>
public class MailgunClient : IDisposable {
    private readonly HttpClient _client;
    /// <summary>Measures total time spent sending.</summary>
    public readonly Stopwatch Stopwatch;

    private string ApiKey => Helpers.CredentialToApiKey(Credentials);
    private string EmailDomain {
        get {
            var address = Helpers.GetEmailAddress(From);
            if (!address.Contains('@')) {
                throw new ArgumentException($"Invalid email address: {address}", nameof(From));
            }
            return address.Split('@')[1];
        }
    }

    /// <summary>Credentials used to authenticate to the API.</summary>
    public ICredentials Credentials { get; set; }
    /// <summary>Determines how errors are handled.</summary>
    public ActionPreference? ErrorAction { get; set; }

    /// <summary>Primary recipients.</summary>
    public List<object> To { get; set; } = new();
    /// <summary>Carbon copy recipients.</summary>
    public List<object> Cc { get; set; } = new();
    /// <summary>Blind carbon copy recipients.</summary>
    public List<object> Bcc { get; set; } = new();
    /// <summary>The sender address.</summary>
    public object From { get; set; }
    /// <summary>Reply-to address.</summary>
    public object? ReplyTo { get; set; }
    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }
    /// <summary>Plain text body.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>HTML body.</summary>
    public string Html { get; set; } = string.Empty;
    /// <summary>File paths to include as attachments.</summary>
    public string[]? Attachment { get; set; }
    /// <summary>File paths to include as inline attachments.</summary>
    public string[]? InlineAttachment { get; set; }

    /// <summary>Collector used to store log entries.</summary>
    public LogCollector LogCollector { get; set; } = new();
    /// <summary>Number of retry attempts on failure.</summary>
    public int RetryCount { get; set; } = 0;
    /// <summary>Base delay in milliseconds between retries.</summary>
    public int RetryDelayMilliseconds { get; set; } = 0;
    /// <summary>Exponential backoff multiplier for retries.</summary>
    public double RetryDelayBackoff { get; set; } = 1.0;

    /// <summary>
    /// When set to <c>true</c> the client retries sending even if the
    /// encountered error is not transient.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    /// <summary>URL of the webhook called after sending.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>The normalized sender email address.</summary>
    public string SentFrom => Helpers.GetEmailAddress(From);
    /// <summary>Comma separated list of recipients.</summary>
    public string SentTo {
        get {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new List<string>();
            if (To != null) addresses.AddRange(Helpers.UniqueAddresses(To, seen).Select(Helpers.GetEmailAddress));
            if (Cc != null) addresses.AddRange(Helpers.UniqueAddresses(Cc, seen).Select(Helpers.GetEmailAddress));
            if (Bcc != null) addresses.AddRange(Helpers.UniqueAddresses(Bcc, seen).Select(Helpers.GetEmailAddress));
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MailgunClient"/> class.
    /// </summary>
    public MailgunClient() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
    }

    /// <summary>
    /// Converts an address object into the format required by the Mailgun API.
    /// </summary>
    /// <param name="address">The address object to convert.</param>
    /// <returns>The formatted address string.</returns>
    private static string ConvertAddress(object address) {
        var (email, name) = Helpers.GetEmailAndName(address);
        return string.IsNullOrWhiteSpace(name) ? email : $"{name} <{email}>";
    }

    private static async Task<byte[]> ReadFileBytesAsync(string path, CancellationToken cancellationToken) {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var bytes = new byte[fs.Length];
        var read = 0;
        while (read < bytes.Length) {
            var r = await fs.ReadAsync(bytes, read, bytes.Length - read, cancellationToken);
            if (r == 0) break;
            read += r;
        }
        return bytes;
    }

    /// <summary>
    /// Builds the multipart HTTP content used for the Mailgun API request.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel asynchronous operations.</param>
    /// <returns>The constructed multipart content.</returns>
    private async Task<MultipartFormDataContent> CreateContentAsync(CancellationToken cancellationToken) {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(ConvertAddress(From)), "from");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in Helpers.UniqueAddresses(To, seen)) content.Add(new StringContent(ConvertAddress(t)), "to");
        foreach (var c in Helpers.UniqueAddresses(Cc, seen)) content.Add(new StringContent(ConvertAddress(c)), "cc");
        foreach (var b in Helpers.UniqueAddresses(Bcc, seen)) content.Add(new StringContent(ConvertAddress(b)), "bcc");
        if (ReplyTo != null) content.Add(new StringContent(ConvertAddress(ReplyTo)), "h:Reply-To");
        if (!string.IsNullOrWhiteSpace(Subject)) content.Add(new StringContent(Subject), "subject");
        if (!string.IsNullOrWhiteSpace(Text)) content.Add(new StringContent(Text), "text");
        if (!string.IsNullOrWhiteSpace(Html)) content.Add(new StringContent(Html), "html");
        if (Attachment != null) {
            foreach (var path in Attachment) {
                if (!File.Exists(path)) {
                    LogCollector.LogWarning($"Send-EmailMessage - Attachment file not found: {path}");
                    LogCollector.LogWarning($"Send-EmailMessage - Possible issue: Path '{path}' is invalid. Verify the file exists and the path is correct.");
                    continue;
                }

                var bytes = await ReadFileBytesAsync(path, cancellationToken);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "attachment", Path.GetFileName(path));
            }
        }
        if (InlineAttachment != null) {
            foreach (var path in InlineAttachment) {
                if (!File.Exists(path)) {
                    LogCollector.LogWarning($"Send-EmailMessage - Inline attachment file not found: {path}");
                    LogCollector.LogWarning($"Send-EmailMessage - Possible issue: Path '{path}' is invalid. Verify the file exists and the path is correct.");
                    continue;
                }

                var bytes = await ReadFileBytesAsync(path, cancellationToken);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "inline", Path.GetFileName(path));
            }
        }
        return content;
    }

    /// <summary>
    /// Sends the email using the Mailgun REST API.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<SmtpResult> SendEmailAsync(CancellationToken cancellationToken = default) {
        var url = $"https://api.mailgun.net/v3/{EmailDomain}/messages";
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{ApiKey}"));

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                using var content = await CreateContentAsync(cancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Post, url) {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                var response = await _client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode) {
                    var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, response.StatusCode.ToString(), "");
                    await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken);
                    return okResult;
                }
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(error);
            } catch (HttpRequestException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Mailgun: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) throw;
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, "", ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }
                var delay = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
            }
            attempts++;
        } while (attempts <= RetryCount);
        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, "", lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return finalResult;
    }

    /// <summary>
    /// Releases resources used by the client.
    /// </summary>
    public void Dispose() {
        _client.Dispose();
    }
}
