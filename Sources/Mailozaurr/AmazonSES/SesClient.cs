using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

/// <summary>
/// Simple client for sending emails using the Amazon SES REST API.
/// </summary>
public class SesClient : IDisposable {
    private readonly HttpClient _client;

    /// <summary>Measures send latency.</summary>
    public readonly Stopwatch Stopwatch;

    /// <summary>Credentials used to authenticate with Amazon SES.</summary>
    public ICredentials Credentials { get; set; } = CredentialCache.DefaultNetworkCredentials;
    /// <summary>Controls how errors are handled.</summary>
    public ActionPreference? ErrorAction { get; set; }

    /// <summary>Primary recipients.</summary>
    public List<object> To { get; set; } = new();
    /// <summary>Carbon copy recipients.</summary>
    public List<object> Cc { get; set; } = new();
    /// <summary>Blind carbon copy recipients.</summary>
    public List<object> Bcc { get; set; } = new();
    /// <summary>The sender address.</summary>
    public object From { get; set; } = string.Empty;
    /// <summary>Reply-to address.</summary>
    public object? ReplyTo { get; set; }
    /// <summary>The message subject.</summary>
    public string? Subject { get; set; }
    /// <summary>Plain text body.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>HTML body.</summary>
    public string Html { get; set; } = string.Empty;
    /// <summary>Paths to attachments to include.</summary>
    public string[]? Attachment { get; set; }
    /// <summary>Paths to inline attachments to include.</summary>
    public string[]? InlineAttachment { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>Number of retry attempts on failure.</summary>
    public int RetryCount { get; set; } = 0;
    /// <summary>Base delay in milliseconds between retries.</summary>
    public int RetryDelayMilliseconds { get; set; } = 0;
    /// <summary>Exponential backoff multiplier for retries.</summary>
    public double RetryDelayBackoff { get; set; } = 1.0;
    /// <summary>Retry even on non-transient errors.</summary>
    public bool RetryAlways { get; set; } = false;

    /// <summary>AWS region to use.</summary>
    public string Region { get; set; } = "us-east-1";
    /// <summary>Webhook invoked after sending.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Collector used to store log entries.</summary>
    public LogCollector LogCollector { get; set; } = new();

    /// <summary>
    /// Gets the normalized sender email address.
    /// </summary>
    public string SentFrom => Helpers.GetEmailAddress(From);

    /// <summary>
    /// Gets a comma separated list of recipient email addresses.
    /// </summary>
    public string SentTo
    {
        get
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<string> addresses = new();
            if (To != null) addresses.AddRange(Helpers.UniqueAddresses(To, seen).Select(Helpers.GetEmailAddress));
            if (Cc != null) addresses.AddRange(Helpers.UniqueAddresses(Cc, seen).Select(Helpers.GetEmailAddress));
            if (Bcc != null) addresses.AddRange(Helpers.UniqueAddresses(Bcc, seen).Select(Helpers.GetEmailAddress));
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SesClient"/> class using a default <see cref="HttpClient"/>.
    /// </summary>
    public SesClient() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SesClient"/> class using the specified HTTP handler.
    /// </summary>
    /// <param name="handler">The HTTP handler to use for requests.</param>
    public SesClient(HttpMessageHandler handler) {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient(handler);
    }

    private MimeMessage BuildMessage()
    {
        Smtp smtp = new();
        smtp.From = From;
        smtp.To = To;
        smtp.Cc = Cc;
        smtp.Bcc = Bcc;
        smtp.ReplyTo = ReplyTo;
        smtp.Subject = Subject ?? string.Empty;
        smtp.TextBody = Text;
        smtp.HtmlBody = Html;
        if (Attachment != null) smtp.Attachments = Attachment.ToList<object>();
        if (InlineAttachment != null) smtp.InlineAttachments = InlineAttachment.ToList<object>();
        if (Headers != null) smtp.Headers = Headers;
        smtp.CreateMessage();
        return smtp.Message;
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using HMACSHA256 hmac = new(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Sha256Hex(string data)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private HttpRequestMessage CreateRequest(string content, DateTime utcNow)
    {
        NetworkCredential net = Credentials as NetworkCredential ?? throw new InvalidCastException("Credentials must be NetworkCredential");
        string accessKey = net.UserName;
        string secretKey = net.Password;
        string service = "ses";
        string region = Region;
        string amzDate = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
        string dateStamp = utcNow.ToString("yyyyMMdd");
        string canonicalHeaders = $"content-type:application/x-www-form-urlencoded\nhost:email.{region}.amazonaws.com\nx-amz-date:{amzDate}\n";
        string signedHeaders = "content-type;host;x-amz-date";
        string payloadHash = Sha256Hex(content);
        string canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        string credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        string stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";
        byte[] kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        byte[] kRegion = HmacSha256(kDate, region);
        byte[] kService = HmacSha256(kRegion, service);
        byte[] kSigning = HmacSha256(kService, "aws4_request");
        byte[] sigBytes = HmacSha256(kSigning, stringToSign);
        string signature = BitConverter.ToString(sigBytes).Replace("-", string.Empty).ToLowerInvariant();
        string authorization = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        HttpRequestMessage request = new(HttpMethod.Post, $"https://email.{region}.amazonaws.com/");
        request.Content = new StringContent(content, Encoding.UTF8, "application/x-www-form-urlencoded");
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return request;
    }

    /// <summary>
    /// Sends the email using Amazon SES.
    /// </summary>
    public async Task<SmtpResult> SendEmailAsync(CancellationToken cancellationToken = default)
    {
        int attempts = 0;
        Exception? lastException = null;
        MimeMessage message = BuildMessage();
        using MemoryStream stream = new();
        await message.WriteToAsync(stream, cancellationToken);
        string raw = Convert.ToBase64String(stream.ToArray());
        string body = $"Action=SendRawEmail&RawMessage.Data={Uri.EscapeDataString(raw)}&Version=2010-12-01";
        do
        {
            try
            {
                using HttpRequestMessage request = CreateRequest(body, DateTime.UtcNow);
                HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
                string respContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    SmtpResult ok = new(true, EmailAction.Send, SentTo, SentFrom, "SESApi", 0, Stopwatch.Elapsed, response.StatusCode.ToString());
                    await Helpers.PostWebhookAsync(WebhookUrl, ok, cancellationToken, _client);
                    return ok;
                }

                lastException = new HttpRequestException(respContent);
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using SES: {respContent}");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using SES: {ex.Message}");
            }

            if ((!Helpers.IsTransient(lastException) && !RetryAlways) || attempts >= RetryCount)
            {
                if (ErrorAction == ActionPreference.Stop && lastException != null)
                {
                    throw lastException;
                }
                SmtpResult fail = new(false, EmailAction.Send, SentTo, SentFrom, "SESApi", 0, Stopwatch.Elapsed, string.Empty, lastException?.Message);
                await Helpers.PostWebhookAsync(WebhookUrl, fail, cancellationToken, _client);
                return fail;
            }

            int delay = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
            if (delay > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
            }
            attempts++;
        }
        while (attempts <= RetryCount);

        SmtpResult final = new(false, EmailAction.Send, SentTo, SentFrom, "SESApi", 0, Stopwatch.Elapsed, string.Empty, lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, final, cancellationToken, _client);
        return final;
    }

    /// <summary>
    /// Releases resources used by the <see cref="SesClient"/> instance.
    /// </summary>
    public void Dispose() {
        _client.Dispose();
    }
}
