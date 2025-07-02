using MailKit;
using MailKit.Net.Imap;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// Provides helper methods for enumerating top-level IMAP folders.
/// </summary>
public static class ImapRootFolderEnumerator {
    /// <summary>
    /// Asynchronously enumerates top-level folders for the given IMAP client.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async IAsyncEnumerable<IMailFolder> EnumerateAsync(
        ImapClient client,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var root = client.PersonalNamespaces.Count > 0
            ? client.GetFolder(client.PersonalNamespaces[0])
            : client.GetFolder("");

        foreach (var folder in await root.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false)) {
            yield return folder;
        }
    }
}
