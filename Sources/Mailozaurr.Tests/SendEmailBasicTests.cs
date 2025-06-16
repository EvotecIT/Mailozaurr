using Xunit;
using Mailozaurr;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using MimeKit;

namespace Mailozaurr.Tests {
    public class SendEmailBasicTests {
        [Fact(Skip="Requires network access")]
        public void SendEmail_Smtp_WithValidInput_Succeeds() {
            // Arrange
            var smtp = new Smtp();
            smtp.From = "sender@example.com";
            smtp.To = new[] { "recipient@example.com" };
            smtp.Subject = "Test Email (SMTP)";
            smtp.HtmlBody = "<b>Hello from Mailozaurr SMTP!</b>";
            smtp.Client.Connect("smtp.example.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            smtp.Authenticate("username", "password", false);

            // Act
            var result = smtp.Send();

            // Assert
            Assert.True(result.Status, $"SMTP send failed: {result.Error}");
        }

        [Fact(Skip="Requires network access")]
        public async Task SendEmail_SendGrid_WithValidInput_Succeeds() {
            // Arrange
            var sendGrid = new SendGridClient();
            sendGrid.From = "sender@example.com";
            sendGrid.To = new System.Collections.Generic.List<object> { "recipient@example.com" };
            sendGrid.Subject = "Test Email (SendGrid)";
            sendGrid.Html = "<b>Hello from Mailozaurr SendGrid!</b>";
            sendGrid.Text = "Hello from Mailozaurr SendGrid!";
            sendGrid.Credentials = new NetworkCredential("apikey", "SENDGRID_API_KEY");
            sendGrid.CreateMessage();

            // Act
            var result = await sendGrid.SendEmailAsync();

            // Assert
            Assert.True(result.Status, $"SendGrid send failed: {result.Error}");
        }

        [Fact(Skip="Requires network access")]
        public async Task SendEmail_Graph_WithValidInput_Succeeds() {
            // Arrange
            var graph = new Graph();
            graph.From = "sender@example.com";
            graph.To = new object[] { "recipient@example.com" };
            graph.Subject = "Test Email (Graph)";
            graph.HTML = "<b>Hello from Mailozaurr Graph!</b>";
            graph.ContentType = "HTML";
            graph.Authenticate(new NetworkCredential("CLIENT_ID@TENANT_ID", "CLIENT_SECRET"));
            await graph.ConnectO365GraphAsync();

            // Act
            var result = await graph.SendMessageAsync();

            // Assert
            Assert.True(result.Status, $"Graph send failed: {result.Error}");
        }

        [Fact]
        public void SendEmail_WithInvalidEmailAddress_Fails() {
            // Arrange
            var result = Validator.ValidateEmail("invalid");

            // Act & Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SendEmail_WithMissingSubjectOrBody_Fails() {
            // Arrange
            var smtp = new Smtp();
            smtp.From = "a@b.com";
            smtp.To = new object[] { "c@d.com" };
            smtp.Subject = string.Empty;
            smtp.TextBody = string.Empty;
            smtp.CreateMessage();

            // Act
            var subjectEmpty = string.IsNullOrEmpty(smtp.Message.Subject);
            var bodyEmpty = smtp.Message.Body is TextPart part && string.IsNullOrEmpty(part.Text);

            // Assert
            Assert.True(subjectEmpty || bodyEmpty);
        }

        [Fact]
        public void SendEmail_WithAttachment_Succeeds() {
            // Arrange
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, "data");
            var smtp = new Smtp();
            smtp.From = "a@b.com";
            smtp.To = new object[] { "c@d.com" };
            smtp.Subject = "test";
            smtp.Attachments = new System.Collections.Generic.List<object> { tmp };
            smtp.CreateMessage();

            // Act
            var attachCount = smtp.Message.BodyParts
                .OfType<MimePart>()
                .Count(p => p.IsAttachment);
            File.Delete(tmp);

            // Assert
            Assert.Equal(1, attachCount);
        }
    }
}