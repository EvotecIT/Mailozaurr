using System.Net.Http.Headers;
using System.Diagnostics;

namespace Mailozaurr;

/// <summary>
/// Simple client for sending emails using the Mailgun API.
/// </summary>
public class MailgunClient : IDisposable {
    private readonly HttpClient _client;
    public readonly Stopwatch Stopwatch;

    private string ApiKey => Helpers.CredentialToApiKey(Credentials);
    private string EmailDomain => Helpers.GetEmailAddress(From).Split('@')[1];

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
    public int RetryCount { get; set; } = 0;
    public int RetryDelayMilliseconds { get; set; } = 0;
    public double RetryDelayBackoff { get; set; } = 1.0;

    public string SentFrom => Helpers.GetEmailAddress(From);
    public string SentTo {
        get {
            var addresses = new List<string>();
            if (To != null) addresses.AddRange(To.Select(Helpers.GetEmailAddress));
            if (Cc != null) addresses.AddRange(Cc.Select(Helpers.GetEmailAddress));
            if (Bcc != null) addresses.AddRange(Bcc.Select(Helpers.GetEmailAddress));
            return string.Join(",", addresses);
        }
    }

    public MailgunClient() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
    }

    private static string ConvertAddress(object address) {
        var (email, name) = Helpers.GetEmailAndName(address);
        return string.IsNullOrEmpty(name) ? email : $"{name} <{email}>";
    }

    private MultipartFormDataContent CreateContent() {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(ConvertAddress(From)), "from");
        foreach (var t in To) content.Add(new StringContent(ConvertAddress(t)), "to");
        foreach (var c in Cc) content.Add(new StringContent(ConvertAddress(c)), "cc");
        foreach (var b in Bcc) content.Add(new StringContent(ConvertAddress(b)), "bcc");
        if (ReplyTo != null) content.Add(new StringContent(ConvertAddress(ReplyTo)), "h:Reply-To");
        if (!string.IsNullOrEmpty(Subject)) content.Add(new StringContent(Subject), "subject");
        if (!string.IsNullOrEmpty(Text)) content.Add(new StringContent(Text), "text");
        if (!string.IsNullOrEmpty(Html)) content.Add(new StringContent(Html), "html");
        if (Attachment != null) {
            foreach (var path in Attachment) {
                var bytes = File.ReadAllBytes(path);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "attachment", Path.GetFileName(path));
            }
        }
        if (InlineAttachment != null) {
            foreach (var path in InlineAttachment) {
                var bytes = File.ReadAllBytes(path);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "inline", Path.GetFileName(path));
            }
        }
        return content;
    }

    public async Task<SmtpResult> SendEmailAsync() {
        var url = $"https://api.mailgun.net/v3/{EmailDomain}/messages";
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{ApiKey}"));

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) {
                    Content = CreateContent()
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                var response = await _client.SendAsync(request);
                if (response.IsSuccessStatusCode) {
                    return new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, response.StatusCode.ToString(), "");
                }
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(error);
            } catch (Exception ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Mailgun: {ex.Message}");
                if (attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) throw;
                    return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, "", ex.Message);
                }
                var delay = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay));
            }
            attempts++;
        } while (attempts <= RetryCount);
        return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, "", lastException?.Message);
    }

    public void Dispose() {
        _client.Dispose();
    }
}
