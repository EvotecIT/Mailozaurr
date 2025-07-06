using System;
using System.Collections.Generic;
using MimeKit;
using MailKit;
using Xunit;

namespace Mailozaurr.Tests;

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
        var sent = DateTime.UtcNow;
        var received = sent.AddMinutes(1);
        var raw = new Dictionary<string, object> {
            ["id"] = "1",
            ["subject"] = "sub",
            ["from"] = new Dictionary<string, object> {
                ["emailAddress"] = new Dictionary<string, object> { ["address"] = "a@example.com" }
            },
            ["sender"] = new Dictionary<string, object> {
                ["emailAddress"] = new Dictionary<string, object> { ["address"] = "s@example.com" }
            },
            ["toRecipients"] = new object[] {
                new Dictionary<string, object> {
                    ["emailAddress"] = new Dictionary<string, object> { ["address"] = "b@example.com" }
                }
            },
            ["ccRecipients"] = new object[] {
                new Dictionary<string, object> {
                    ["emailAddress"] = new Dictionary<string, object> { ["address"] = "c@example.com" }
                }
            },
            ["sentDateTime"] = sent.ToString("o"),
            ["receivedDateTime"] = received.ToString("o"),
            ["bodyPreview"] = "preview",
            ["body"] = new Dictionary<string, object> {
                ["content"] = "gcontent"
            },
            ["isRead"] = true,
            ["hasAttachments"] = true,
            ["conversationId"] = "conv",
            ["internetMessageId"] = "<id@ex>",
            ["importance"] = "high"
        };
        var info = new GraphMessageInfo(raw, "user@example.com");
        Assert.Equal("1", info.Id);
        Assert.Equal("user@example.com", info.UserPrincipalName);
        Assert.Contains("a@example.com", info.From!);
        Assert.Contains("s@example.com", info.Sender!);
        Assert.Contains("b@example.com", info.To!);
        Assert.Contains("c@example.com", info.Cc!);
        Assert.Equal("sub", info.Subject);
        Assert.Same(raw, info.Raw);
        Assert.Equal("preview", info.BodyPreview);
        Assert.Equal("gcontent", info.Content);
        Assert.True(info.IsRead);
        Assert.True(info.HasAttachments);
        Assert.Equal("conv", info.ConversationId);
        Assert.Equal("<id@ex>", info.InternetMessageId);
        Assert.Equal(received, info.ReceivedDate);

        Assert.Equal("high", info.Importance);
    }
    [Fact]
    public void GraphFolderInfo_ExposesProperties() {
        var raw = new Dictionary<string, object> {
            ["id"] = "fid",
            ["displayName"] = "Inbox",
            ["parentFolderId"] = "root",
            ["childFolderCount"] = 3
        };
        var info = new GraphFolderInfo(raw, "user@example.com");
        Assert.Equal("fid", info.Id);
        Assert.Equal("Inbox", info.DisplayName);
        Assert.Equal("root", info.ParentFolderId);
        Assert.Equal(3, info.ChildFolderCount);
        Assert.Same(raw, info.Raw);
        Assert.Equal("user@example.com", info.UserPrincipalName);
    }
}
