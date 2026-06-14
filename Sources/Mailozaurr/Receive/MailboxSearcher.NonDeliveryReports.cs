using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using Mailozaurr.DmarcReports;
using Mailozaurr.NonDeliveryReports;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public static partial class MailboxSearcher {
    /// <summary>
    /// Searches for Non-Delivery Reports in an IMAP mailbox.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Optional folder to search. Defaults to the inbox.</param>
    /// <param name="since">Optional UTC lower bound for report timestamps.</param>
    /// <param name="before">Optional UTC upper bound for report timestamps.</param>
    /// <param name="recipientContains">Optional string that the recipient should contain.</param>
    /// <param name="messageId">Optional original message id to match.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        ImapClient client,
        string? folder = null,
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        var search = BuildNonDeliveryReportSearchQuery(since, before);
        var uids = await mailFolder.SearchAsync(search, cancellationToken).ConfigureAwait(false);
        var results = new List<NonDeliveryReport>();
        if (parallelDownloadLimit <= 1) {
            foreach (var uid in uids) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
                var reports = FilterNonDeliveryReports(new[] { msg }, since, before, recipientContains, messageId);
                if (reports.Count > 0) {
                    foreach (var r in reports) {
                        if (maxResults > 0 && results.Count >= maxResults) break;
                        results.Add(r);
                    }
                    if (maxResults > 0 && results.Count >= maxResults) break;
                }
            }
        } else {
            var uidArray = uids.ToArray();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var workers = new Task[Math.Min(parallelDownloadLimit, uidArray.Length)];
            int next = 0;
            int resultCount = 0;
            var gate = new object();
            for (int i = 0; i < workers.Length; i++) {
                workers[i] = Task.Run(async () => {
                    while (true) {
                        int current;
                        lock (gate) {
                            if (cts.IsCancellationRequested || next >= uidArray.Length || (maxResults > 0 && resultCount >= maxResults)) return;
                            current = next++;
                        }
                        MimeMessage msg;
                        try {
                            msg = await mailFolder.GetMessageAsync(uidArray[current], cts.Token).ConfigureAwait(false);
                        } catch (OperationCanceledException) {
                            return;
                        }
                        var reports = FilterNonDeliveryReports(new[] { msg }, since, before, recipientContains, messageId);
                        if (reports.Count == 0) continue;
                        lock (gate) {
                            foreach (var r in reports) {
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                                results.Add(r);
                                resultCount++;
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                            }
                        }
                    }
                }, CancellationToken.None);
            }
            try {
                await Task.WhenAll(workers).ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        }

        return maxResults > 0 && results.Count > maxResults ? results.GetRange(0, maxResults) : results;
    }

    /// <summary>
    /// Searches for Non-Delivery Reports in a POP3 mailbox.
    /// </summary>
    /// <param name="client">Connected POP3 client.</param>
    /// <param name="since">Optional UTC lower bound for report timestamps.</param>
    /// <param name="before">Optional UTC upper bound for report timestamps.</param>
    /// <param name="recipientContains">Optional string that the recipient should contain.</param>
    /// <param name="messageId">Optional original message id to match.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        Pop3Client client,
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        var results = new List<NonDeliveryReport>();
        if (parallelDownloadLimit <= 1) {
            for (int idx = 0; idx < client.Count; idx++) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await client.GetMessageAsync(idx, cancellationToken).ConfigureAwait(false);
                var reports = FilterNonDeliveryReports(new[] { msg }, since, before, recipientContains, messageId);
                if (reports.Count > 0) {
                    foreach (var r in reports) {
                        if (maxResults > 0 && results.Count >= maxResults) break;
                        results.Add(r);
                    }
                    if (maxResults > 0 && results.Count >= maxResults) break;
                }
            }
        } else {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var workers = new Task[Math.Min(parallelDownloadLimit, client.Count)];
            int next = 0;
            int resultCount = 0;
            var gate = new object();
            for (int i = 0; i < workers.Length; i++) {
                workers[i] = Task.Run(async () => {
                    while (true) {
                        int current;
                        lock (gate) {
                            if (cts.IsCancellationRequested || next >= client.Count || (maxResults > 0 && resultCount >= maxResults)) return;
                            current = next++;
                        }
                        MimeMessage msg;
                        try {
                            msg = await client.GetMessageAsync(current, cts.Token).ConfigureAwait(false);
                        } catch (OperationCanceledException) {
                            return;
                        }
                        var reports = FilterNonDeliveryReports(new[] { msg }, since, before, recipientContains, messageId);
                        if (reports.Count == 0) continue;
                        lock (gate) {
                            foreach (var r in reports) {
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                                results.Add(r);
                                resultCount++;
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                            }
                        }
                    }
                }, CancellationToken.None);
            }
            try {
                await Task.WhenAll(workers).ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        }

        return maxResults > 0 && results.Count > maxResults ? results.GetRange(0, maxResults) : results;
    }

    /// <summary>
    /// Searches for Non-Delivery Reports using Microsoft Graph.
    /// </summary>
    /// <param name="credential">Graph credential used to access the mailbox.</param>
    /// <param name="userPrincipalName">UPN of the mailbox to search.</param>
    /// <param name="since">Optional UTC lower bound for report timestamps.</param>
    /// <param name="before">Optional UTC upper bound for report timestamps.</param>
    /// <param name="recipientContains">Optional string that the recipient should contain.</param>
    /// <param name="messageId">Optional original message id to match.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        GraphCredential credential,
        string userPrincipalName,
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        var filters = new List<string>();
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        var subjectFilters = new List<string>();
        foreach (var pattern in NonDeliveryReportSubjectPatterns.Values) {
            subjectFilters.Add($"contains(subject,'{pattern.Replace("'", "''")}')");
        }
        if (subjectFilters.Count > 0) filters.Add($"({string.Join(" or ", subjectFilters)})");
        if (sinceUtc.HasValue) filters.Add($"receivedDateTime ge {sinceUtc.Value:o}");
        if (beforeUtc.HasValue) filters.Add($"receivedDateTime le {beforeUtc.Value:o}");
        var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
        var msgs = await MicrosoftGraphUtils.GetMailMessagesAsync(
            credential,
            userPrincipalName,
            new[] { "id" },
            filter,
            maxResults > 0 ? maxResults : (int?)null,
            cancellationToken).ConfigureAwait(false);
        var results = new List<NonDeliveryReport>();
        if (parallelDownloadLimit <= 1) {
            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (maxResults > 0 && results.Count >= maxResults) break;
                if (m.TryGetValue("id", out var idObj) && idObj is string id) {
                    var mime = await MicrosoftGraphUtils.GetMailMessageMimeAsync(credential, userPrincipalName, id, cancellationToken).ConfigureAwait(false);
                    var reports = FilterNonDeliveryReports(new[] { mime }, since, before, recipientContains, messageId);
                    if (reports.Count > 0) {
                        foreach (var r in reports) {
                            if (maxResults > 0 && results.Count >= maxResults) break;
                            results.Add(r);
                        }
                        if (maxResults > 0 && results.Count >= maxResults) break;
                    }
                }
            }
        } else {
            var msgArray = msgs.ToArray();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var workers = new Task[Math.Min(parallelDownloadLimit, msgArray.Length)];
            int next = 0;
            int resultCount = 0;
            var gate = new object();
            for (int i = 0; i < workers.Length; i++) {
                workers[i] = Task.Run(async () => {
                    while (true) {
                        Dictionary<string, object>? current;
                        lock (gate) {
                            if (cts.IsCancellationRequested || next >= msgArray.Length || (maxResults > 0 && resultCount >= maxResults)) return;
                            current = msgArray[next++];
                        }
                        if (!current.TryGetValue("id", out var idObj) || idObj is not string id) continue;
                        MimeMessage mime;
                        try {
                            cts.Token.ThrowIfCancellationRequested();
                            mime = await MicrosoftGraphUtils.GetMailMessageMimeAsync(credential, userPrincipalName, id, cts.Token).ConfigureAwait(false);
                        } catch (OperationCanceledException) {
                            return;
                        }
                        var reports = FilterNonDeliveryReports(new[] { mime }, since, before, recipientContains, messageId);
                        if (reports.Count == 0) continue;
                        lock (gate) {
                            foreach (var r in reports) {
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                                results.Add(r);
                                resultCount++;
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                            }
                        }
                    }
                }, CancellationToken.None);
            }
            try {
                await Task.WhenAll(workers).ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        }

        return maxResults > 0 && results.Count > maxResults ? results.GetRange(0, maxResults) : results;
    }

    /// <summary>
    /// Searches for Non-Delivery Reports using the Gmail API.
    /// </summary>
    /// <param name="client">Initialized Gmail API client.</param>
    /// <param name="userId">User mailbox identifier (e.g. "me").</param>
    /// <param name="since">Optional UTC lower bound for report timestamps.</param>
    /// <param name="before">Optional UTC upper bound for report timestamps.</param>
    /// <param name="recipientContains">Optional string that the recipient should contain.</param>
    /// <param name="messageId">Optional original message id to match.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<NonDeliveryReport>> SearchNonDeliveryReportsAsync(
        GmailApiClient client,
        string userId,
        DateTime? since = null,
        DateTime? before = null,
        string? recipientContains = null,
        string? messageId = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        string query = BuildGmailNonDeliveryReportQuery(since, before);
        var msgs = await client.ListAsync(userId, query, maxResults > 0 ? maxResults : (int?)null, cancellationToken).ConfigureAwait(false);
        var results = new List<NonDeliveryReport>();
        if (parallelDownloadLimit <= 1) {
            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (maxResults > 0 && results.Count >= maxResults) break;
                if (!string.IsNullOrEmpty(m.Id)) {
                    var mime = await client.GetMimeMessageAsync(userId, m.Id!, cancellationToken).ConfigureAwait(false);
                    var reports = FilterNonDeliveryReports(new[] { mime }, since, before, recipientContains, messageId);
                    if (reports.Count > 0) {
                        foreach (var r in reports) {
                            if (maxResults > 0 && results.Count >= maxResults) break;
                            results.Add(r);
                        }
                        if (maxResults > 0 && results.Count >= maxResults) break;
                    }
                }
            }
        } else {
            var msgArray = msgs.ToArray();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var workers = new Task[Math.Min(parallelDownloadLimit, msgArray.Length)];
            int next = 0;
            int resultCount = 0;
            var gate = new object();
            for (int i = 0; i < workers.Length; i++) {
                workers[i] = Task.Run(async () => {
                    while (true) {
                        GmailMessage? current;
                        lock (gate) {
                            if (cts.IsCancellationRequested || next >= msgArray.Length || (maxResults > 0 && resultCount >= maxResults)) return;
                            current = msgArray[next++];
                        }
                        if (string.IsNullOrEmpty(current.Id)) continue;
                        MimeMessage mime;
                        try {
                            mime = await client.GetMimeMessageAsync(userId, current.Id!, cts.Token).ConfigureAwait(false);
                        } catch (OperationCanceledException) {
                            return;
                        }
                        var reports = FilterNonDeliveryReports(new[] { mime }, since, before, recipientContains, messageId);
                        if (reports.Count == 0) continue;
                        lock (gate) {
                            foreach (var r in reports) {
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                                results.Add(r);
                                resultCount++;
                                if (maxResults > 0 && resultCount >= maxResults) {
                                    cts.Cancel();
                                    break;
                                }
                            }
                        }
                    }
                }, CancellationToken.None);
            }
            try {
                await Task.WhenAll(workers).ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        }

        return maxResults > 0 && results.Count > maxResults ? results.GetRange(0, maxResults) : results;
    }

    // DMARC report helpers
}