using MimeKit;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class Pop3AttachmentPayloadBuilderTests {
    [Fact]
    public void Build_ReturnsNotFound_WhenIndexOutOfRange() {
        var message = new MimeMessage {
            Body = new TextPart("plain") { Text = "body" }
        };

        var result = Pop3AttachmentPayloadBuilder.Build(message, attachmentIndex: 0, maxBytes: 1024);

        Assert.Equal(Pop3AttachmentBuildStatus.NotFound, result.Status);
        Assert.False(result.Success);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Build_ReturnsSizeLimitExceeded_WhenPayloadTooLarge() {
        var message = new MimeMessage();
        var body = new BodyBuilder { TextBody = "body" };
        body.Attachments.Add("a.txt", new byte[64]);
        message.Body = body.ToMessageBody();

        var result = Pop3AttachmentPayloadBuilder.Build(message, attachmentIndex: 0, maxBytes: 8);

        Assert.Equal(Pop3AttachmentBuildStatus.SizeLimitExceeded, result.Status);
        Assert.False(result.Success);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Build_ReturnsPayload_ForMimePart() {
        var message = new MimeMessage();
        var body = new BodyBuilder { TextBody = "body" };
        body.Attachments.Add("a.txt", new byte[] { 1, 2, 3, 4 });
        message.Body = body.ToMessageBody();

        var result = Pop3AttachmentPayloadBuilder.Build(message, attachmentIndex: 0, maxBytes: 1024);

        Assert.Equal(Pop3AttachmentBuildStatus.Success, result.Status);
        Assert.True(result.Success);
        Assert.NotNull(result.Payload);
        Assert.Equal("a.txt", result.Payload!.FileName);
        Assert.Equal(4, result.Payload.Bytes.Length);
    }
}