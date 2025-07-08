using Mailozaurr;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

public static class SendEmailHeadersSendGrid
{
    public static async Task RunAsync()
    {
        var sendGrid = new SendGridClient();
        sendGrid.From = "sender@example.com";
        sendGrid.To = new List<object> { "recipient@example.com" };
        sendGrid.Subject = "SendGrid Headers";
        sendGrid.Html = "<p>Hello</p>";
        sendGrid.Headers = new Dictionary<string, string>
        {
            ["X-Tracking-ID"] = "abc123",
            ["X-Source"] = "Mailozaurr"
        };
        sendGrid.Credentials = new NetworkCredential("apikey", "sg-key");
        await sendGrid.SendEmailAsync();
    }
}
