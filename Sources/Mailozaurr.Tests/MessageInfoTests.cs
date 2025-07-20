using System;
using System.Collections.Generic;
using MimeKit;
using MailKit;
using Mailozaurr;
using Xunit;

namespace Mailozaurr.Tests;

/// <summary>
/// Tests for message information helper classes.
/// </summary>
public class MessageInfoTests {
    [Fact]
    public void ImapInfo_ExposesProperties() {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("From", "from@example.com"));
        msg.To.Add(new MailboxAddress("To", "to@example.com"));
        msg.Subject = "subj";
        msg.Date = DateTimeOffset.UtcNow;
        msg.Body = new TextPart("plain") { Text = "content" };
        var raw = new ImapEmailMessage(new UniqueId(1), msg);
        var info = new ImapMessageInfo(raw);
        Assert.Equal(1u, info.Uid.Id);
        Assert.Contains("from@example.com", info.From);
        Assert.Equal("subj", info.Subject);
        Assert.Same(raw, info.Raw);
        Assert.Equal("content", info.TextBody);
    }

    [Fact]
    public void Pop3Info_ExposesProperties() {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress("F", "f@example.com"));
        msg.To.Add(new MailboxAddress("T", "t@example.com"));
        msg.Subject = "hello";
        msg.Date = DateTimeOffset.UtcNow;
        msg.Body = new TextPart("plain") { Text = "pcontent" };
        var raw = new Pop3EmailMessage(5, msg);
        var info = new Pop3MessageInfo(raw);
        Assert.Equal(5, info.Index);
        Assert.Contains("f@example.com", info.From);
        Assert.Equal("hello", info.Subject);
        Assert.Same(raw, info.Raw);
        Assert.Equal("pcontent", info.TextBody);
    }

    [Fact]
    public void GraphInfo_ExposesProperties() {
        var raw = new Dictionary<string, object> {
            ["id"] = "1",
            ["subject"] = "sub",
            ["from"] = new Dictionary<string, object> {
                ["emailAddress"] = new Dictionary<string, object> { ["address"] = "a@example.com" }
            },
            ["toRecipients"] = new object[] {
                new Dictionary<string, object> {
                    ["emailAddress"] = new Dictionary<string, object> { ["address"] = "b@example.com" }
                }
            },
            ["sentDateTime"] = DateTime.UtcNow.ToString("o"),
            ["bodyPreview"] = "preview",
            ["body"] = new Dictionary<string, object> {
                ["content"] = "gcontent"
            },
            ["isRead"] = true,
            ["importance"] = "high"
        };
        var info = new GraphMessageInfo(raw, "user@example.com");
        Assert.Equal("1", info.Id);
        Assert.Equal("user@example.com", info.UserPrincipalName);
        Assert.Contains("a@example.com", info.From!);
        Assert.Contains("b@example.com", info.To!);
        Assert.Equal("sub", info.Subject);
        Assert.Same(raw, info.Raw);
        Assert.Equal("preview", info.BodyPreview);
        Assert.Equal("gcontent", info.Content);
        Assert.True(info.IsRead);
        Assert.Equal(GraphImportance.High, info.Importance);
    }
}
