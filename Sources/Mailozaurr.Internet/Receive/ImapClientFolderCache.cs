using MailKit;
using MailKit.Net.Imap;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Mailozaurr;

/// <summary>
/// Provides folder caching for <see cref="ImapClient"/> instances.
/// </summary>
public static class ImapClientFolderCache {
    private static readonly ConditionalWeakTable<ImapClient, ConcurrentDictionary<string, IMailFolder>> Cache = new();

    /// <summary>
    /// Retrieves a folder, caching it on first access and ensuring it is opened with the specified access.
    /// </summary>
    /// <param name="client">IMAP client instance.</param>
    /// <param name="folder">Folder name or null for the inbox.</param>
    /// <param name="access">Folder access mode.</param>
    /// <returns>The opened folder.</returns>
    public static IMailFolder GetCachedFolder(this ImapClient client, string? folder, FolderAccess access) {
        var map = Cache.GetOrCreateValue(client);
        var inbox = client.Inbox ?? throw new InvalidOperationException("The IMAP client does not expose an inbox folder.");
        var inboxName = inbox.FullName ?? inbox.Name ?? "INBOX";
        var name = string.IsNullOrWhiteSpace(folder) ? inboxName : folder!;

        if (!map.TryGetValue(name, out var mailFolder)) {
            if (name.Equals(inboxName, System.StringComparison.OrdinalIgnoreCase)) {
                mailFolder = inbox;
            } else {
                try {
                    mailFolder = client.GetFolder(name);
                } catch (FolderNotFoundException) {
                    if (client.PersonalNamespaces.Count == 0) {
                        throw;
                    }

                    mailFolder = client.GetFolder(client.PersonalNamespaces[0]).GetSubfolder(name);
                }
            }
            map[name] = mailFolder;
        }

        if (!mailFolder.IsOpen || mailFolder.Access != access) {
            mailFolder.Open(access);
        }

        return mailFolder;
    }

    /// <summary>
    /// Clears cached folders for the specified client.
    /// </summary>
    /// <param name="client">IMAP client instance.</param>
    public static void ClearFolderCache(this ImapClient client) => Cache.Remove(client);
}