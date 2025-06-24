using Mailozaurr;

using MimeKit;

using System.Collections.Generic;



public static class SendEmailAttachments

{

    public static void Run()

    {

        var smtp = new Smtp();

        smtp.From = "sender@example.com";

        smtp.To = new[] { "recipient@example.com" };

        smtp.Subject = "Attachment Demo";

        smtp.TextBody = "Check attachments";

        smtp.Attachments = new List<object> { "C:\\Temp\\report.pdf" };

        var part = new MimePart("text/plain")

        {

            Content = new MimeContent(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("Hello from memory"))),

            FileName = "Memory.txt"

        };

        smtp.Attachments.Add(part);

        smtp.InlineAttachments = new List<object> { "C:\\Temp\\logo.png" };

        smtp.Connect("smtp.example.com", 25);

        smtp.Send();

        smtp.Disconnect();

    }

}

