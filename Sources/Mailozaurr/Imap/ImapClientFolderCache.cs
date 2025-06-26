using MailKit;
using MailKit.Net.Imap;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mailozaurr;

public static class ImapClientFolderCache
{
    private static readonly ConditionalWeakTable<ImapClient, Dictionary<string, ImapFolder>> _cache = new();

    public static ImapFolder GetOrOpenFolder(this ImapClient client, string? folderName, FolderAccess access)
    {
        var folders = _cache.GetOrCreateValue(client);
        var key = string.IsNullOrEmpty(folderName) ? client.Inbox.FullName : folderName!;
        if (!folders.TryGetValue(key, out var folder))
        {
            folder = string.IsNullOrEmpty(folderName) ? (ImapFolder)client.Inbox : GetFolder(client, folderName!);
            folders[key] = folder;
        }
        if (!folder.IsOpen)
        {
            folder.Open(access);
        }
        return folder;
    }

    public static void ClearFolderCache(this ImapClient client)
    {
        _cache.Remove(client);
    }

    private static ImapFolder GetFolder(ImapClient client, string folder)
    {
        try
        {
            return (ImapFolder)client.GetFolder(folder);
        }
        catch
        {
            return (ImapFolder)client.GetFolder(client.PersonalNamespaces[0]).GetSubfolder(folder);
        }
    }
}
