using Mailozaurr;
using Mailozaurr.Definitions;

public static class SendEmailVerifyAttachments {
    public static void Run() {
        var smtp = new Smtp {
            From = "sender@example.com",
            To = new[] { "recipient@example.com" },
            Subject = "Verify attachments demo",
            TextBody = "body",
            Attachments = new List<AttachmentDescriptor> { new FileAttachmentDescriptor("missing-file.txt") }
        };

        smtp.Connect("smtp.example.com", 25);
        smtp.Send();
        smtp.Disconnect();
    }
}