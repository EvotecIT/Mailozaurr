using System.Threading.Tasks;
using System.Collections.Generic;
using MailKit;
using MailKit.Security;
using MimeKit;
using Xunit;

namespace Mailozaurr.Tests {
#if NET8_0
    public class FetchExamplesTests {
        private class FakeImapFolder
        {
            public bool OpenCalled;
            public bool ExpungeCalled;
            public List<int> Fetched { get; } = new();

            public Task OpenAsync(FolderAccess access)
            {
                OpenCalled = true;
                return Task.CompletedTask;
            }

            public Task<IList<int>> SearchAsync(object query)
                => Task.FromResult<IList<int>>(new List<int> { 1, 2 });

            public Task<MimeMessage> GetMessageAsync(int id)
            {
                Fetched.Add(id);
                return Task.FromResult(new MimeMessage());
            }

            public Task AddFlagsAsync(int id, MessageFlags flags, bool silent)
                => Task.CompletedTask;

            public Task ExpungeAsync()
            {
                ExpungeCalled = true;
                return Task.CompletedTask;
            }
        }

        private class FakeImapClient
        {
            public bool ConnectCalled;
            public bool AuthCalled;
            public bool DisconnectCalled;
            public FakeImapFolder Folder { get; } = new();

            public Task ConnectAsync(string host, int port, SecureSocketOptions options)
            {
                ConnectCalled = true;
                return Task.CompletedTask;
            }

            public Task AuthenticateAsync(string user, string pass)
            {
                AuthCalled = true;
                return Task.CompletedTask;
            }

            public FakeImapFolder GetFolder(string name) => Folder;

            public Task DisconnectAsync(bool quit)
            {
                DisconnectCalled = true;
                return Task.CompletedTask;
            }
        }

        private class FakePop3Client
        {
            public bool ConnectCalled;
            public bool AuthCalled;
            public bool DisconnectCalled;
            public int Count { get; set; } = 2;
            public List<int> Deleted { get; } = new();

            public Task ConnectAsync(string host, int port, SecureSocketOptions options)
            {
                ConnectCalled = true;
                return Task.CompletedTask;
            }

            public Task AuthenticateAsync(string user, string pass)
            {
                AuthCalled = true;
                return Task.CompletedTask;
            }

            public Task<MimeMessage> GetMessageAsync(int index)
                => Task.FromResult(new MimeMessage());

            public Task DeleteMessageAsync(int index)
            {
                Deleted.Add(index);
                return Task.CompletedTask;
            }

            public Task DisconnectAsync(bool quit)
            {
                DisconnectCalled = true;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task FetchImapExample_Runs() {
            var client = new FakeImapClient();
            await client.ConnectAsync("imap.example.com", 993, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync("user@example.com", "Pa55w0rd");
            var folder = client.GetFolder("Inbox/Reports");
            await folder.OpenAsync(FolderAccess.ReadWrite);
            var uids = await folder.SearchAsync(null);
            foreach (var uid in uids) {
                _ = await folder.GetMessageAsync(uid);
                await folder.AddFlagsAsync(uid, MessageFlags.Deleted, true);
            }
            if (uids.Count > 0) {
                await folder.ExpungeAsync();
            }
            await client.DisconnectAsync(true);

            Assert.True(client.ConnectCalled);
            Assert.True(client.AuthCalled);
            Assert.True(client.DisconnectCalled);
            Assert.Equal(new[] { 1, 2 }, folder.Fetched);
            Assert.True(folder.ExpungeCalled);
        }

        [Fact]
        public async Task FetchPopExample_Runs() {
            var client = new FakePop3Client();
            await client.ConnectAsync("pop.example.com", 995, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync("user@example.com", "Pa55w0rd");
            for (int i = 0; i < client.Count; i++) {
                _ = await client.GetMessageAsync(i);
                await client.DeleteMessageAsync(i);
            }
            await client.DisconnectAsync(true);

            Assert.True(client.ConnectCalled);
            Assert.True(client.AuthCalled);
            Assert.True(client.DisconnectCalled);
            Assert.Equal(new[] { 0, 1 }, client.Deleted);
        }
    }
#endif
}
