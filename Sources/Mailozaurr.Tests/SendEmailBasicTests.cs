using Xunit;
using Mailozaurr.Definitions;
using Mailozaurr;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Management.Automation;
using MimeKit;

namespace Mailozaurr.Tests {
    public class SendEmailBasicTests {
        private class FakeSmtpClient : ClientSmtp
        {
            public bool SendCalled;
            public MimeMessage? LastMessage;

            public override Task<string> SendAsync(MimeMessage message, System.Threading.CancellationToken cancellationToken = default, MailKit.ITransferProgress? progress = null)
            {
                SendCalled = true;
                LastMessage = message;
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
        public void SendEmail_Smtp_WithAutoCreateMessage_BuildsMessage() {
            var smtp = new Smtp { AutoCreateMessage = true };
            var fake = new FakeSmtpClient();
            var field = typeof(Smtp).GetField("<Client>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            field.SetValue(smtp, fake);

            smtp.From = "sender@example.com";
            smtp.To = new[] { "recipient@example.com" };
            smtp.Subject = "Test Email (SMTP)";
            smtp.HtmlBody = "<b>Hello from Mailozaurr SMTP!</b>";

            var result = smtp.Send();

            Assert.True(result.Status, $"SMTP send failed: {result.Error}");
            Assert.True(fake.SendCalled);
            Assert.NotNull(fake.LastMessage);
            Assert.NotEmpty(fake.LastMessage!.From);
        }

        [Fact]
        public void SendEmail_Smtp_WithAutoCreateMessage_PreservesCustomHeaders() {
            var smtp = new Smtp { AutoCreateMessage = true };
            var fake = new FakeSmtpClient();
            var field = typeof(Smtp).GetField("<Client>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            field.SetValue(smtp, fake);

            smtp.Message.Headers.Add("X-Test", "1");
            smtp.From = "sender@example.com";
            smtp.To = new[] { "recipient@example.com" };
            smtp.Subject = "Test Email (SMTP)";
            smtp.HtmlBody = "<b>Hello from Mailozaurr SMTP!</b>";

            var result = smtp.Send();

            Assert.True(result.Status, $"SMTP send failed: {result.Error}");
            Assert.NotNull(fake.LastMessage);
            Assert.Equal("1", fake.LastMessage!.Headers["X-Test"]);
        }

        [Fact]
        public void SendEmail_Smtp_WithoutCreateMessage_ReturnsHelpfulError() {
            var smtp = new Smtp();
            var fake = new FakeSmtpClient();
            var field = typeof(Smtp).GetField("<Client>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            field.SetValue(smtp, fake);

            smtp.From = "sender@example.com";
            smtp.To = new[] { "recipient@example.com" };
            smtp.Subject = "Test Email (SMTP)";
            smtp.HtmlBody = "<b>Hello from Mailozaurr SMTP!</b>";

            var result = smtp.Send();

            Assert.False(result.Status);
            Assert.Contains("CreateMessage", result.Error);
            Assert.False(fake.SendCalled);
        }

        [Fact]
        public void SendEmail_Smtp_WithErrorActionStopAndMissingSender_Throws() {
            var smtp = new Smtp {
                AutoCreateMessage = true,
                ErrorAction = ActionPreference.Stop
            };
            var fake = new FakeSmtpClient();
            var field = typeof(Smtp).GetField("<Client>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            field.SetValue(smtp, fake);

            smtp.To = new[] { "recipient@example.com" };
            smtp.Subject = "Missing sender";
            smtp.HtmlBody = "<b>Hello</b>";

            var ex = Assert.Throws<InvalidOperationException>(() => smtp.Send());
            Assert.Contains("no sender", ex.Message, System.StringComparison.OrdinalIgnoreCase);
            Assert.False(fake.SendCalled);
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
        public async Task SendEmail_SendGrid_WithToken_Succeeds() {
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

            using var cts = new CancellationTokenSource();
            var result = await client.SendEmailAsync(cts.Token);

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
        public async Task SendEmail_SendGrid_ServerError_Fails() {
            var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("err") });
            var client = new SendGridClient();
            var field = typeof(SendGridClient).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(client, new HttpClient(handler));

            client.From = "sender@example.com";
            client.To = new System.Collections.Generic.List<object> { "recipient@example.com" };
            client.Subject = "Test Email (SendGrid Failure)";
            client.Html = "<b>Body</b>";
            client.Text = "Body";
            client.Credentials = new NetworkCredential("apikey", "SENDGRID_API_KEY");
            client.CreateMessage();

            var result = await client.SendEmailAsync();

            Assert.False(result.Status);
            Assert.Single(handler.Requests);
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
        public async Task ConnectO365GraphAsync_GraphCanceled_ThrowsOperationCanceledException() {
            using var graph = new Graph();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            graph.Authenticate(new NetworkCredential("client@tenant", "secret"));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => graph.ConnectO365GraphAsync(cts.Token));
        }

        [Fact]
        public async Task SendEmail_GraphCanceled_ThrowsOperationCanceledException() {
            using var graph = new Graph {
                From = "sender@example.com",
                To = new object[] { "recipient@example.com" },
                Subject = "Test Email (Graph Cancel)",
                HTML = "<b>Hello from Mailozaurr Graph!</b>",
                ContentType = "HTML",
                AccessToken = "token",
                TokenType = "Bearer"
            };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => graph.SendMessageAsync(cts.Token));
        }

        [Fact]
        public async Task SendDraftEmail_GraphCanceled_ThrowsOperationCanceledException() {
            using var graph = new Graph {
                From = "sender@example.com",
                To = new object[] { "recipient@example.com" },
                Subject = "Test Draft Email (Graph Cancel)",
                HTML = "<b>Hello from Mailozaurr Graph Draft!</b>",
                ContentType = "HTML",
                AccessToken = "token",
                TokenType = "Bearer"
            };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => graph.SendMessageDraftAsync(cts.Token));
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
            smtp.Attachments = new System.Collections.Generic.List<AttachmentDescriptor> { new FileAttachmentDescriptor(tmp) };
            smtp.CreateMessage();

            // Act
            var attachCount = smtp.Message.BodyParts
                .OfType<MimePart>()
                .Count(p => p.IsAttachment);
            File.Delete(tmp);

            // Assert
            Assert.Equal(1, attachCount);
        }

        [Fact]
        public void SendEmail_WithDuplicateAttachments_AddsOnce() {
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, "data");
            var smtp = new Smtp();
            smtp.From = "a@b.com";
            smtp.To = new object[] { "c@d.com" };
            smtp.Subject = "test";
            smtp.Attachments = new System.Collections.Generic.List<AttachmentDescriptor>
            {
                new FileAttachmentDescriptor(tmp),
                new FileAttachmentDescriptor(tmp)
            };
            smtp.CreateMessage();

            var attachCount = smtp.Message.BodyParts
                .OfType<MimePart>()
                .Count(p => p.IsAttachment);
            File.Delete(tmp);

            Assert.Equal(1, attachCount);
        }
    }}
