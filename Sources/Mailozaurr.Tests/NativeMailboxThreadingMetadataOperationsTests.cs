using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests;

public sealed class NativeMailboxThreadingMetadataOperationsTests {
    [Fact]
    public void Normalize_FromRawValues_NormalizesAndDeduplicates() {
        var metadata = NativeMailboxThreadingMetadataOperations.Normalize(
            messageId: " <child@example.test> ",
            replyTo: " reply@example.test ",
            cc: " cc@example.test ",
            inReplyTo: " <parent@example.test> ",
            references: new[] {
                "<root@example.test>",
                "parent@example.test",
                " <ROOT@example.test> ",
                "  "
            });

        Assert.Equal("child@example.test", metadata.MessageId);
        Assert.Equal("reply@example.test", metadata.ReplyTo);
        Assert.Equal("cc@example.test", metadata.Cc);
        Assert.Equal("parent@example.test", metadata.InReplyTo);
        Assert.NotNull(metadata.References);
        Assert.Equal(2, metadata.References!.Count);
        Assert.Equal("root@example.test", metadata.References[0]);
        Assert.Equal("parent@example.test", metadata.References[1]);
    }

    [Fact]
    public void Normalize_FromGraphResult_UsesSharedNormalization() {
        var graph = new GraphMailboxBrowser.GraphMailboxThreadingMetadataResult {
            MessageId = " <graph@example.test> ",
            ReplyTo = " graph-reply@example.test ",
            Cc = " graph-cc@example.test ",
            InReplyTo = " <graph-parent@example.test> ",
            References = new List<string> {
                "<graph-root@example.test>",
                "graph-parent@example.test",
                "<GRAPH-ROOT@example.test>"
            }
        };

        var metadata = NativeMailboxThreadingMetadataOperations.Normalize(graph);

        Assert.Equal("graph@example.test", metadata.MessageId);
        Assert.Equal("graph-reply@example.test", metadata.ReplyTo);
        Assert.Equal("graph-cc@example.test", metadata.Cc);
        Assert.Equal("graph-parent@example.test", metadata.InReplyTo);
        Assert.NotNull(metadata.References);
        Assert.Equal(2, metadata.References!.Count);
    }

    [Fact]
    public void Normalize_FromGmailResult_UsesSharedNormalization() {
        var gmail = new GmailMailboxBrowser.GmailMailboxThreadingMetadataResult {
            MessageId = " <gmail@example.test> ",
            ReplyTo = " gmail-reply@example.test ",
            Cc = " gmail-cc@example.test ",
            InReplyTo = " <gmail-parent@example.test> ",
            References = new List<string> {
                "<gmail-root@example.test>",
                "gmail-parent@example.test",
                "<GMAIL-ROOT@example.test>"
            }
        };

        var metadata = NativeMailboxThreadingMetadataOperations.Normalize(gmail);

        Assert.Equal("gmail@example.test", metadata.MessageId);
        Assert.Equal("gmail-reply@example.test", metadata.ReplyTo);
        Assert.Equal("gmail-cc@example.test", metadata.Cc);
        Assert.Equal("gmail-parent@example.test", metadata.InReplyTo);
        Assert.NotNull(metadata.References);
        Assert.Equal(2, metadata.References!.Count);
    }

    [Fact]
    public void Normalize_EmptyReferences_ReturnsNullReferences() {
        var metadata = NativeMailboxThreadingMetadataOperations.Normalize(
            messageId: null,
            replyTo: null,
            cc: null,
            inReplyTo: null,
            references: new[] { " ", "\t" });

        Assert.Null(metadata.MessageId);
        Assert.Null(metadata.ReplyTo);
        Assert.Null(metadata.Cc);
        Assert.Null(metadata.InReplyTo);
        Assert.Null(metadata.References);
    }
}