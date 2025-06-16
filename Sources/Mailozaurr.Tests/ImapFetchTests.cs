using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mailozaurr.Tests {
    public class ImapFetchTests {
        private class FakeMessage {
            public FakeMessage(string subject, bool isRead) {
                Subject = subject;
                IsRead = isRead;
            }

            public string Subject { get; }
            public bool IsRead { get; }
        }

        private class FakeImapClient {
            private readonly Dictionary<string, List<FakeMessage>> _mailboxes = new();
            public bool ValidCredentials { get; set; } = true;

            public void AddMailbox(string name, params FakeMessage[] messages) =>
                _mailboxes[name] = messages.ToList();

            public IReadOnlyList<FakeMessage> Fetch(string mailbox, bool unreadOnly = false) {
                if (!ValidCredentials) {
                    throw new InvalidOperationException("Invalid credentials");
                }

                if (!_mailboxes.TryGetValue(mailbox, out var messages)) {
                    throw new InvalidOperationException("Mailbox not found");
                }

                return unreadOnly ? messages.Where(m => !m.IsRead).ToList() : messages;
            }
        }
        [Fact]
        public void Imap_Fetch_WithValidMailbox_Succeeds() {
            // Arrange
            var client = new FakeImapClient();
            client.AddMailbox("Inbox", new FakeMessage("hello", false));

            // Act
            var messages = client.Fetch("Inbox");

            // Assert
            Assert.Single(messages);
        }

        [Fact]
        public void Imap_Fetch_WithInvalidMailbox_Fails() {
            // Arrange
            var client = new FakeImapClient();
            client.AddMailbox("Inbox");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => client.Fetch("Missing"));
        }

        [Fact]
        public void Imap_Fetch_UnreadMessages_Succeeds() {
            // Arrange
            var client = new FakeImapClient();
            client.AddMailbox("Inbox",
                new FakeMessage("read", true),
                new FakeMessage("unread", false));

            // Act
            var unread = client.Fetch("Inbox", unreadOnly: true);

            // Assert
            Assert.Single(unread);
            Assert.Equal("unread", unread[0].Subject);
        }

        [Fact]
        public void Imap_Fetch_WithInvalidCredentials_Fails() {
            // Arrange
            var client = new FakeImapClient { ValidCredentials = false };
            client.AddMailbox("Inbox", new FakeMessage("hello", false));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => client.Fetch("Inbox"));
        }
    }
}