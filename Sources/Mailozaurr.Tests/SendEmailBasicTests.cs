using Xunit;
using Mailozaurr;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using MimeKit;

namespace Mailozaurr.Tests {
    public class SendEmailBasicTests {
        private class FakeSmtpClient : ClientSmtp
        {
            public bool SendCalled;

            public override Task<string> SendAsync(MimeMessage message, System.Threading.CancellationToken cancellationToken = default, MailKit.ITransferProgress? progress = null)
            {
                SendCalled = true;
                return Task.FromResult(string.Empty);
            }
        }

        [Fact]
        public void SendEmail_Smtp_WithValidInput_Succeeds() {
            var smtp = new Smtp();
            var fake = new FakeSmtpClient();
            var field = typeof(Smtp).GetField("<Client>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            field.SetValue(smtp, fake);

            smtp.From = "sender@example.com";
            smtp.To = new[] { "recipient@example.com" };
            smtp.Subject = "Test Email (SMTP)";
            smtp.HtmlBody = "<b>Hello from Mailozaurr SMTP!</b>";
            smtp.CreateMessage();

            var result = smtp.Send();

            Assert.True(result.Status, $"SMTP send failed: {result.Error}");
            Assert.True(fake.SendCalled);
        }

        [Fact]
        public async Task SendEmail_SendGrid_WithValidInput_Succeeds() {
            var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
            var client = new SendGridClient();
            var field = typeof(SendGridClient).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(client, new HttpClient(handler));

            client.From = "sender@example.com";
            client.To = new System.Collections.Generic.List<object> { "recipient@example.com" };
            client.Subject = "Test Email (SendGrid)";
            client.Html = "<b>Hello from Mailozaurr SendGrid!</b>";
            client.Text = "Hello from Mailozaurr SendGrid!";
            client.Credentials = new NetworkCredential("apikey", "SENDGRID_API_KEY");
            client.CreateMessage();

            var result = await client.SendEmailAsync();

            Assert.True(result.Status, $"SendGrid send failed: {result.Error}");
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task SendEmail_SendGrid_InvalidCredentialType_Fails() {
            var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
            var client = new SendGridClient();
            var field = typeof(SendGridClient).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(client, new HttpClient(handler));

            client.From = "sender@example.com";
            client.To = new System.Collections.Generic.List<object> { "recipient@example.com" };
            client.Subject = "Test Email (SendGrid)";
            client.Html = "<b>Hello from Mailozaurr SendGrid!</b>";
            client.Text = "Hello from Mailozaurr SendGrid!";
            client.Credentials = new CredentialCache();
            client.CreateMessage();

            var result = await client.SendEmailAsync();

            Assert.False(result.Status);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task SendEmail_Graph_WithValidInput_Succeeds() {
            var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
            using var graph = new Graph();
            var field = typeof(Graph).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(graph, new HttpClient(handler));

            graph.From = "sender@example.com";
            graph.To = new object[] { "recipient@example.com" };
            graph.Subject = "Test Email (Graph)";
            graph.HTML = "<b>Hello from Mailozaurr Graph!</b>";
            graph.ContentType = "HTML";
            graph.AccessToken = "token";
            graph.TokenType = "Bearer";

            var result = await graph.SendMessageAsync();

            Assert.True(result.Status, $"Graph send failed: {result.Error}");
            Assert.Single(handler.Requests);
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
            var subjectEmpty = string.IsNullOrWhiteSpace(smtp.Message.Subject);
            var bodyEmpty = smtp.Message.Body is TextPart part && string.IsNullOrWhiteSpace(part.Text);

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