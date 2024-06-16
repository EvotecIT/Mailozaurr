namespace Mailozaurr;

public class MailgunClient : IDisposable {
    private readonly HttpClient _client;

    /// <summary>
    /// Gets the API key used for authentication with the Mailgun API.
    /// </summary>
    private string _apiKey => Helpers.CredentialToApiKey(Credentials);

    /// <summary>
    /// Gets or sets the domain of the email address.
    /// </summary>
    private string EmailDomain {
        get {
            var emailInformation = From.Split("@");
            return emailInformation[1];
        }
    }

    /// <summary>
    /// Gets or sets the credentials used for authentication with the Mailgun API.
    /// </summary>
    public ICredentials Credentials { get; set; }

    /// <summary>
    /// Gets or sets the action to take when an error occurs.
    /// </summary>
    public ActionPreference? ErrorAction { get; set; }

    public object[]? Bcc { get; set; }
    public object[]? Cc { get; set; }
    public object From { get; set; }
    public object[]? To { get; set; }
    public string Subject { get; set; }
    public string BodyText { get; set; }
    public string BodyHtml { get; set; }


    public MailgunClient(ICredentials credentials, object from, object[] to, object[] cc, object[] bcc, string subject, string bodyText, string bodyHtml) {
        _client = new HttpClient();
        Credentials = credentials;
        From = from;
        To = to;
        Subject = subject;
        BodyText = bodyText;
        BodyHtml = bodyHtml;
    }

    public async Task<bool> SendEmailAsync() {
        var url = $"https://api.mailgun.net/v3/{EmailDomain}/messages";
        var idpass = $"api:{_apiKey}";
        var basicauth = Convert.ToBase64String(Encoding.ASCII.GetBytes(idpass));
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicauth);

        var formData = new Dictionary<string, string> {
                { "from", From },
                { "to", string.Join(",", To) },
                { "subject", Subject },
                { "text", BodyText },
                { "html", BodyHtml }
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await _client.PostAsync(url, content);

        return response.IsSuccessStatusCode;
    }

    public void Dispose() {
        _client?.Dispose();
    }
}
