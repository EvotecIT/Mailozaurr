using Mailozaurr;

/// <summary>
/// Example showing how to embed remote images automatically.
/// </summary>
public static class SendEmailRemoteImages
{
    public static void Run()
    {
        var smtp = new Smtp { AutoEmbedRemoteImages = true };
        smtp.From = "sender@example.com";
        smtp.To = new[] { "recipient@example.com" };
        smtp.Subject = "Remote Images";
        smtp.HtmlBody = "<img src=\"https://example.com/logo.png\">";
        smtp.Connect("smtp.example.com", 25);
        smtp.Send();
        smtp.Disconnect();
    }
}
