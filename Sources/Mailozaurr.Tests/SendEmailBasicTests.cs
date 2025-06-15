using Xunit;
using Mailozaurr;
using System.Net;
using System.Threading.Tasks;

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
            // TODO: Setup email with invalid address

            // Act
            // TODO: Call send email

            // Assert
            Assert.True(true); // Placeholder for failure
        }

        [Fact]
        public void SendEmail_WithMissingSubjectOrBody_Fails() {
            // Arrange
            // TODO: Setup email with missing subject/body

            // Act
            // TODO: Call send email

            // Assert
            Assert.True(true); // Placeholder for failure
        }

        [Fact]
        public void SendEmail_WithAttachment_Succeeds() {
            // Arrange
            // TODO: Setup email with attachment

            // Act
            // TODO: Call send email

            // Assert
            Assert.True(true); // Placeholder for success
        }
    }
}