using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using MimeKit;
using System.Text;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr.NonDeliveryReports;
using Mailozaurr.DmarcReports;

namespace Mailozaurr;

/// <summary>
/// Provides mailbox search helpers for IMAP and POP3.
/// </summary>
public static class MailboxSearcher {
    /// <summary>
    /// Searches an IMAP mailbox and returns matching messages.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Optional folder to search. Defaults to the inbox.</param>
    /// <param name="subject">Optional "subject contains" filter.</param>
    /// <param name="fromContains">Optional "from contains" filter.</param>
    /// <param name="toContains">Optional "to contains" filter.</param>
    /// <param name="bodyContains">Optional "body contains" filter.</param>
    /// <param name="priority">Optional message priority to match.</param>
    /// <param name="since">Optional UTC lower bound for message delivery dates.</param>
    /// <param name="before">Optional UTC upper bound for message delivery dates.</param>
    /// <param name="hasAttachment">If true, only messages with attachments are returned.</param>
    /// <param name="additionalQueries">Additional IMAP search queries to combine.</param>
    /// <param name="maxResults">Maximum number of results to return. Use 0 for unlimited.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <param name="queryString">Optional free-form query string parsed into filters.</param>
    public static async Task<IList<ImapEmailMessage>> SearchImapAsync(
        ImapClient client,
        string? folder = null,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        string? bodyContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool hasAttachment = false,
        IEnumerable<SearchQuery>? additionalQueries = null,
        int maxResults = 0,
        CancellationToken cancellationToken = default,
        string? queryString = null) {
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        SearchQuery search = SearchQuery.All;
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (!string.IsNullOrWhiteSpace(subject)) search = search.And(SearchQuery.SubjectContains(subject!));
        if (!string.IsNullOrWhiteSpace(fromContains)) search = search.And(SearchQuery.FromContains(fromContains!));
        if (!string.IsNullOrWhiteSpace(toContains)) search = search.And(SearchQuery.ToContains(toContains!));
        if (!string.IsNullOrWhiteSpace(bodyContains)) search = search.And(SearchQuery.BodyContains(bodyContains!));
        if (sinceUtc.HasValue) search = search.And(SearchQuery.DeliveredAfter(sinceUtc.Value));
        if (beforeUtc.HasValue) search = search.And(SearchQuery.DeliveredBefore(beforeUtc.Value));
        if (additionalQueries != null) {
            foreach (var q in additionalQueries) {
                if (q != null) search = search.And(q);
            }
        }
          if (!string.IsNullOrWhiteSpace(queryString)) {
              try {
                  var parsed = ParseQuery(queryString);
                  if (!string.IsNullOrWhiteSpace(parsed.Subject)) search = search.And(SearchQuery.SubjectContains(parsed.Subject!));
                  if (!string.IsNullOrWhiteSpace(parsed.FromContains)) search = search.And(SearchQuery.FromContains(parsed.FromContains!));
                  if (!string.IsNullOrWhiteSpace(parsed.ToContains)) search = search.And(SearchQuery.ToContains(parsed.ToContains!));
                  if (!string.IsNullOrWhiteSpace(parsed.BodyContains)) search = search.And(SearchQuery.BodyContains(parsed.BodyContains!));
                  var parsedSince = NormalizeToUtc(parsed.Since);
                  var parsedBefore = NormalizeToUtc(parsed.Before);
                  if (parsedSince.HasValue) search = search.And(SearchQuery.DeliveredAfter(parsedSince.Value));
                  if (parsedBefore.HasValue) search = search.And(SearchQuery.DeliveredBefore(parsedBefore.Value));
                  foreach (var q in parsed.AdditionalQueries) search = search.And(q);
                  hasAttachment |= parsed.HasAttachment;
                  if (!priority.HasValue) priority = parsed.Priority;
              } catch (Exception ex) {
                  LoggingMessages.Logger.WriteWarning("Failed to parse IMAP query string: {0}", ex.Message);
              }
        }
        var uids = await mailFolder.SearchAsync(search, cancellationToken).ConfigureAwait(false);
        var result = new List<ImapEmailMessage>(uids.Count);
        foreach (var uid in uids) {
            var message = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            if (hasAttachment && !message.Attachments.Any()) continue;
            if (priority.HasValue && message.Priority != ConvertPriority(priority.Value)) continue;
            result.Add(new ImapEmailMessage(uid, message));
            if (maxResults > 0 && result.Count >= maxResults) break;
        }
        return result;
    }

    /// <summary>
    /// Searches a POP3 mailbox and returns matching messages.
    /// </summary>
    /// <param name="client">Connected POP3 client.</param>
    /// <param name="subject">Optional "subject contains" filter.</param>
    /// <param name="fromContains">Optional "from contains" filter.</param>
    /// <param name="toContains">Optional "to contains" filter.</param>
    /// <param name="bodyContains">Optional "body contains" filter.</param>
    /// <param name="priority">Optional message priority to match.</param>
    /// <param name="since">Optional UTC lower bound for message delivery dates.</param>
    /// <param name="before">Optional UTC upper bound for message delivery dates.</param>
    /// <param name="hasAttachment">If true, only messages with attachments are returned.</param>
    /// <param name="maxResults">Maximum number of results to return. Use 0 for unlimited.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <param name="queryString">Optional free-form query string parsed into filters.</param>
    public static async Task<IList<Pop3EmailMessage>> SearchPop3Async(
        Pop3Client client,
        string? subject = null,
        string? fromContains = null,
        string? toContains = null,
        string? bodyContains = null,
        MessagePriority? priority = null,
        DateTime? since = null,
        DateTime? before = null,
        bool hasAttachment = false,
        int maxResults = 0,
        CancellationToken cancellationToken = default,
        string? queryString = null) {
        if (!string.IsNullOrWhiteSpace(queryString)) {
              try {
                  var parsed = ParseQuery(queryString);
                  if (string.IsNullOrWhiteSpace(subject)) subject = parsed.Subject;
                  if (string.IsNullOrWhiteSpace(fromContains)) fromContains = parsed.FromContains;
                  if (string.IsNullOrWhiteSpace(toContains)) toContains = parsed.ToContains;
                  if (string.IsNullOrWhiteSpace(bodyContains)) bodyContains = parsed.BodyContains;
                  if (!since.HasValue) since = parsed.Since;
                  if (!before.HasValue) before = parsed.Before;
                  if (!priority.HasValue) priority = parsed.Priority;
                  hasAttachment |= parsed.HasAttachment;
              } catch (Exception ex) {
                  LoggingMessages.Logger.WriteWarning("Failed to parse POP3 query string: {0}", ex.Message);
              }
        }
        var results = new List<Pop3EmailMessage>();
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        for (int i = 0; i < client.Count; i++) {
            var message = await client.GetMessageAsync(i, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(subject) && (message.Subject == null || message.Subject.IndexOf(subject, StringComparison.OrdinalIgnoreCase) < 0)) continue;
              if (!string.IsNullOrWhiteSpace(fromContains) && !AddressMatches(message.From, fromContains!)) continue;
              if (!string.IsNullOrWhiteSpace(toContains) && !AddressMatches(message.To, toContains!)) continue;
            if (!string.IsNullOrWhiteSpace(bodyContains)) {
                var textBody = message.TextBody ?? string.Empty;
                var htmlBody = message.HtmlBody ?? string.Empty;
                if (textBody.IndexOf(bodyContains, StringComparison.OrdinalIgnoreCase) < 0 &&
                    htmlBody.IndexOf(bodyContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
            }
            var msgDate = message.Date.UtcDateTime;
            if (sinceUtc.HasValue && msgDate < sinceUtc.Value) continue;
            if (beforeUtc.HasValue && msgDate > beforeUtc.Value) continue;
            if (priority.HasValue && message.Priority != ConvertPriority(priority.Value)) continue;
            if (hasAttachment && !message.Attachments.Any()) continue;
            results.Add(new Pop3EmailMessage(i, message));
            if (maxResults > 0 && results.Count >= maxResults) break;
        }
        return results;
    }

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

    /// <summary>
    /// Searches for DMARC aggregate reports in an IMAP mailbox.
    /// </summary>
    /// <param name="client">Connected IMAP client.</param>
    /// <param name="folder">Optional folder to search. Defaults to the inbox.</param>
    /// <param name="since">Optional UTC lower bound for message dates.</param>
    /// <param name="before">Optional UTC upper bound for message dates.</param>
    /// <param name="domain">Optional domain that report subjects should contain.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="maxUncompressedSize">Maximum uncompressed attachment size to inspect, in bytes.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        ImapClient client,
        string? folder = null,
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        long maxUncompressedSize = 10 * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        var search = BuildDmarcReportSearchQuery(since, before, domain);
        var uids = await mailFolder.SearchAsync(search, cancellationToken).ConfigureAwait(false);
        var results = new List<DmarcReport>();
        if (parallelDownloadLimit <= 1) {
            foreach (var uid in uids) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await mailFolder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
                var reports = FilterDmarcReports(new[] { msg }, since, before, domain, maxUncompressedSize);
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
                        var reports = FilterDmarcReports(new[] { msg }, since, before, domain, maxUncompressedSize);
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
    /// Searches for DMARC aggregate reports in a POP3 mailbox.
    /// </summary>
    /// <param name="client">Connected POP3 client.</param>
    /// <param name="since">Optional UTC lower bound for message dates.</param>
    /// <param name="before">Optional UTC upper bound for message dates.</param>
    /// <param name="domain">Optional domain that report subjects should contain.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="maxUncompressedSize">Maximum uncompressed attachment size to inspect, in bytes.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        Pop3Client client,
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        long maxUncompressedSize = 10 * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        var results = new List<DmarcReport>();
        if (parallelDownloadLimit <= 1) {
            for (int idx = 0; idx < client.Count; idx++) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await client.GetMessageAsync(idx, cancellationToken).ConfigureAwait(false);
                var reports = FilterDmarcReports(new[] { msg }, since, before, domain, maxUncompressedSize);
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
                        var reports = FilterDmarcReports(new[] { msg }, since, before, domain, maxUncompressedSize);
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
    /// Searches for DMARC aggregate reports using Microsoft Graph.
    /// </summary>
    /// <param name="credential">Graph credential used to access the mailbox.</param>
    /// <param name="userPrincipalName">UPN of the mailbox to search.</param>
    /// <param name="since">Optional UTC lower bound for message dates.</param>
    /// <param name="before">Optional UTC upper bound for message dates.</param>
    /// <param name="domain">Optional domain that report subjects should contain.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="maxUncompressedSize">Maximum uncompressed attachment size to inspect, in bytes.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        GraphCredential credential,
        string userPrincipalName,
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        long maxUncompressedSize = 10 * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        var filters = new List<string> { "hasAttachments eq true", "contains(subject,'report domain')" };
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) filters.Add($"receivedDateTime ge {sinceUtc.Value:o}");
        if (beforeUtc.HasValue) filters.Add($"receivedDateTime le {beforeUtc.Value:o}");
        if (!string.IsNullOrWhiteSpace(domain)) filters.Add($"contains(subject,'{domain!.Replace("'", "''")}')");
        var filter = string.Join(" and ", filters);
        var msgs = await MicrosoftGraphUtils.GetMailMessagesAsync(
            credential,
            userPrincipalName,
            new[] { "id" },
            filter,
            maxResults > 0 ? maxResults : (int?)null,
            cancellationToken).ConfigureAwait(false);
        var mimeMessages = new List<MimeMessage>(msgs.Count);
        if (parallelDownloadLimit <= 1) {
            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (m.TryGetValue("id", out var idObj) && idObj is string id) {
                    var mime = await MicrosoftGraphUtils.GetMailMessageMimeAsync(credential, userPrincipalName, id, cancellationToken).ConfigureAwait(false);
                    mimeMessages.Add(mime);
                }
            }
        } else {
            using var semaphore = new SemaphoreSlim(parallelDownloadLimit);
            var tasks = new List<Task>();

            async Task DownloadMessageAsync(string id) {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    var mime = await MicrosoftGraphUtils.GetMailMessageMimeAsync(credential, userPrincipalName, id, cancellationToken).ConfigureAwait(false);
                    lock (mimeMessages) mimeMessages.Add(mime);
                } finally {
                    semaphore.Release();
                }
            }

            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (m.TryGetValue("id", out var idObj) && idObj is string id) {
                    tasks.Add(DownloadMessageAsync(id));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        return FilterDmarcReports(mimeMessages, since, before, domain, maxUncompressedSize);
    }

    /// <summary>
    /// Searches for DMARC aggregate reports using the Gmail API.
    /// </summary>
    /// <param name="client">Initialized Gmail API client.</param>
    /// <param name="userId">User mailbox identifier (e.g. "me").</param>
    /// <param name="since">Optional UTC lower bound for message dates.</param>
    /// <param name="before">Optional UTC upper bound for message dates.</param>
    /// <param name="domain">Optional domain that report subjects should contain.</param>
    /// <param name="maxResults">Optional limit for the number of reports returned. Use 0 for unlimited.</param>
    /// <param name="parallelDownloadLimit">Maximum number of concurrent message downloads.</param>
    /// <param name="maxUncompressedSize">Maximum uncompressed attachment size to inspect, in bytes.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        GmailApiClient client,
        string userId,
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        long maxUncompressedSize = 10 * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        string query = BuildGmailDmarcReportQuery(since, before, domain);
        var msgs = await client.ListAsync(userId, query, maxResults > 0 ? maxResults : (int?)null, cancellationToken).ConfigureAwait(false);
        var mimeMessages = new List<MimeMessage>(msgs.Count);
        if (parallelDownloadLimit <= 1) {
            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(m.Id)) {
                    var mime = await client.GetMimeMessageAsync(userId, m.Id!, cancellationToken).ConfigureAwait(false);
                    mimeMessages.Add(mime);
                    if (maxResults > 0 && mimeMessages.Count >= maxResults) break;
                }
            }
        } else {
            using var semaphore = new SemaphoreSlim(parallelDownloadLimit);
            var tasks = new List<Task>();

            async Task DownloadMessageAsync(string id) {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    var mime = await client.GetMimeMessageAsync(userId, id, cancellationToken).ConfigureAwait(false);
                    lock (mimeMessages) mimeMessages.Add(mime);
                } finally {
                    semaphore.Release();
                }
            }

            foreach (var m in msgs) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(m.Id)) {
                    tasks.Add(DownloadMessageAsync(m.Id!));
                }
                if (maxResults > 0 && tasks.Count >= maxResults) break;
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        return FilterDmarcReports(mimeMessages, since, before, domain, maxUncompressedSize);
    }

    /// <summary>
    /// Filters DMARC reports by date and domain.
    /// </summary>
    /// <param name="messages">Collection of MIME messages to inspect.</param>
    /// <param name="since">Optional UTC lower bound for the message date.</param>
    /// <param name="before">Optional UTC upper bound for the message date.</param>
    /// <param name="domain">Optional domain that report attachments should match.</param>
    /// <param name="maxUncompressedSize">Maximum uncompressed attachment size to inspect, in bytes.</param>
    internal static IList<DmarcReport> FilterDmarcReports(
        IEnumerable<MimeMessage> messages,
        DateTime? since,
        DateTime? before,
        string? domain,
        long maxUncompressedSize = 10 * 1024 * 1024) {
        var results = new List<DmarcReport>();
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        foreach (var message in messages) {
            var msgDate = message.Date.UtcDateTime;
            if (sinceUtc.HasValue && msgDate < sinceUtc.Value) continue;
            if (beforeUtc.HasValue && msgDate > beforeUtc.Value) continue;
            var report = new DmarcReport {
                From = message.From.Mailboxes.FirstOrDefault()?.Address,
                Subject = message.Subject,
                Date = message.Date
            };
            var domainMatched = string.IsNullOrWhiteSpace(domain);
            foreach (var att in message.Attachments) {
                if (IsDmarcAttachment(att) && att is MimePart part) {
                    if (!string.IsNullOrWhiteSpace(domain) && !AttachmentMatchesDomain(part, domain!, maxUncompressedSize)) continue;
                    if (part.Content == null) {
                        continue;
                    }

                    var stream = part.Content.Open();
                    report.Attachments.Add(new DmarcReportAttachment(part.FileName ?? "report.zip", stream));
                    if (!domainMatched) domainMatched = true;
                }
            }
            if (report.Attachments.Count > 0 && domainMatched) results.Add(report);
        }
        return results;
    }

    private static bool AttachmentMatchesDomain(MimePart part, string domain, long maxUncompressedSize) {
        if (part.FileName?.IndexOf(domain, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (part.Content == null) {
            return false;
        }

        try {
            using var stream = part.Content.Open();
            var name = part.FileName ?? string.Empty;
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                foreach (var entry in zip.Entries) {
                    if (entry.Length > maxUncompressedSize) {
                        LoggingMessages.Logger.WriteError("Zip entry {0} exceeds max size {1}", entry.FullName, maxUncompressedSize);
                        continue;
                    }
                    using var entryStream = entry.Open();
                    if (XmlStreamContainsDomain(entryStream, domain, maxUncompressedSize)) return true;
                }
            } else if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) {
                using var gz = new GZipStream(stream, CompressionMode.Decompress);
                if (XmlStreamContainsDomain(gz, domain, maxUncompressedSize)) return true;
            } else {
                if (XmlStreamContainsDomain(stream, domain, maxUncompressedSize)) return true;
            }
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("Failed to process attachment {0}: {1}", part.FileName ?? string.Empty, ex.Message);
        }
        return false;
    }

    private static bool XmlStreamContainsDomain(Stream stream, string domain, long maxUncompressedSize) {
        var settings = new System.Xml.XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, CloseInput = true };
        using var reader = System.Xml.XmlReader.Create(new LimitedStream(stream, maxUncompressedSize), settings);
        while (reader.Read()) {
            if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.LocalName.Equals("domain", StringComparison.OrdinalIgnoreCase)) {
                var value = reader.ReadElementContentAsString();
                if (value.Equals(domain, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    internal static SearchQuery BuildDmarcReportSearchQuery(DateTime? since, DateTime? before, string? domain) {
        SearchQuery search = SearchQuery.SubjectContains("report domain");
        var hasAtt = typeof(SearchQuery).GetProperty("HasAttachment", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null) as SearchQuery;
        search = search.And(hasAtt ?? SearchQuery.HeaderContains("Content-Disposition", "attachment"));
        if (!string.IsNullOrWhiteSpace(domain)) search = search.And(SearchQuery.SubjectContains(domain!));
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) search = search.And(SearchQuery.DeliveredAfter(sinceUtc.Value));
        if (beforeUtc.HasValue) search = search.And(SearchQuery.DeliveredBefore(beforeUtc.Value));
        return search;
    }

    private static string EscapeGmailQueryValue(string value) {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value) {
            if (c == '\\' || c == '"' || c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    internal static string BuildGmailDmarcReportQuery(DateTime? since, DateTime? before, string? domain) {
        var sb = new StringBuilder("subject:\"report domain\" has:attachment");
        if (!string.IsNullOrWhiteSpace(domain)) sb.Append(' ').Append("subject:\"").Append(EscapeGmailQueryValue(domain!)).Append("\"");
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) sb.Append(' ').Append("after:").Append(sinceUtc.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture));
        if (beforeUtc.HasValue) sb.Append(' ').Append("before:").Append(beforeUtc.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture));
        return sb.ToString().Trim();
    }

    private static bool IsDmarcAttachment(MimeEntity entity) {
        if (entity is MimePart part) {
            var name = part.FileName;
            if (!string.IsNullOrEmpty(name) && (name!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))) return true;
            var ct = part.ContentType;
            if (ct != null) {
                if (ct.MediaType.Equals("application", StringComparison.OrdinalIgnoreCase)) {
                    if (ct.MediaSubtype.Equals("zip", StringComparison.OrdinalIgnoreCase) || ct.MediaSubtype.Equals("gzip", StringComparison.OrdinalIgnoreCase) || ct.MediaSubtype.Equals("xml", StringComparison.OrdinalIgnoreCase)) return true;
                } else if (ct.MediaType.Equals("text", StringComparison.OrdinalIgnoreCase)) {
                    if (ct.MediaSubtype.Equals("xml", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }
        return false;
    }


    /// <summary>
    /// Filters Non-Delivery Reports by date, recipient and message id.
    /// </summary>
    /// <param name="messages">Collection of MIME messages to inspect.</param>
    /// <param name="since">Optional UTC lower bound for the report timestamp.</param>
    /// <param name="before">Optional UTC upper bound for the report timestamp.</param>
    /// <param name="recipientContains">Optional string that the recipient should contain.</param>
    /// <param name="messageId">Optional original message id to match.</param>
    internal static IList<NonDeliveryReport> FilterNonDeliveryReports(
        IEnumerable<MimeMessage> messages,
        DateTime? since,
        DateTime? before,
        string? recipientContains,
        string? messageId) {
        messageId = NonDeliveryReport.NormalizeMessageId(messageId);
        var results = new List<NonDeliveryReport>();
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        foreach (var message in messages) {
            foreach (var report in MimeKitUtils.GetNonDeliveryReports(message)) {
                var reportDate = (report.LastAttemptDate ?? report.Timestamp).UtcDateTime;
                if (sinceUtc.HasValue && reportDate < sinceUtc.Value) continue;
                if (beforeUtc.HasValue && reportDate > beforeUtc.Value) continue;
                if (!string.IsNullOrWhiteSpace(recipientContains) && !RecipientMatches(report, recipientContains!)) continue;
                if (!string.IsNullOrWhiteSpace(messageId) && !string.Equals(report.OriginalMessageId, messageId, StringComparison.OrdinalIgnoreCase)) continue;
                results.Add(report);
            }
        }
        return results;
    }

    internal static string BuildGmailNonDeliveryReportQuery(DateTime? since, DateTime? before) {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var pattern in NonDeliveryReportSubjectPatterns.Values) {
            if (!first) sb.Append(" OR ");
            sb.Append("subject:\"").Append(EscapeGmailQueryValue(pattern)).Append("\"");
            first = false;
        }
        if (sb.Length > 0) {
            sb.Insert(0, "(");
            sb.Append(')');
        }
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) sb.Append(' ').Append("after:").Append(sinceUtc.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture));
        if (beforeUtc.HasValue) sb.Append(' ').Append("before:").Append(beforeUtc.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture));
        return sb.ToString().Trim();
    }

      private static bool RecipientMatches(NonDeliveryReport report, string filter) {
          var final = report.FinalRecipientAddress;
          if (final != null &&
              final.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
          var original = report.OriginalRecipientAddress;
          if (original != null &&
              original.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
          return false;
      }

    private static MimeKit.MessagePriority ConvertPriority(MessagePriority priority)
        => priority switch {
            MessagePriority.High => MimeKit.MessagePriority.Urgent,
            MessagePriority.Low => MimeKit.MessagePriority.NonUrgent,
            _ => MimeKit.MessagePriority.Normal,
        };

    private static bool AddressMatches(InternetAddressList list, string filter) {
        foreach (var addr in list.Mailboxes) {
            if (!string.IsNullOrWhiteSpace(addr.Address) &&
                addr.Address.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var displayName = addr.Name;
            if (!string.IsNullOrWhiteSpace(displayName) && displayName!.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    internal static SearchQuery BuildNonDeliveryReportSearchQuery(DateTime? since, DateTime? before) {
        SearchQuery search = SearchQuery.HeaderContains("Content-Type", "delivery-status");
        SearchQuery? subjectQuery = null;
        foreach (var pattern in NonDeliveryReportSubjectPatterns.Values) {
            var q = SearchQuery.SubjectContains(pattern);
            subjectQuery = subjectQuery == null ? q : subjectQuery.Or(q);
        }
        if (subjectQuery != null) search = search.Or(subjectQuery);
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        if (sinceUtc.HasValue) search = search.And(SearchQuery.DeliveredAfter(sinceUtc.Value));
        if (beforeUtc.HasValue) search = search.And(SearchQuery.DeliveredBefore(beforeUtc.Value));
        return search;
    }

    internal sealed class ParsedQuery {
        public string? Subject { get; set; }
        public string? FromContains { get; set; }
        public string? ToContains { get; set; }
        public string? BodyContains { get; set; }
        public MessagePriority? Priority { get; set; }
        public DateTime? Since { get; set; }
        public DateTime? Before { get; set; }
        public bool HasAttachment { get; set; }
        public List<SearchQuery> AdditionalQueries { get; } = new();
    }

      internal static ParsedQuery ParseQuery(string? query) {
          var result = new ParsedQuery();
          if (string.IsNullOrWhiteSpace(query)) return result;
          foreach (var token in SplitTokens(query!)) {
            var parts = token.Split(new[] { ':' }, 2);
            if (parts.Length == 2) {
                var key = parts[0];
                var value = Unquote(parts[1]);
                if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase)) {
                    result.FromContains = value;
                } else if (string.Equals(key, "to", StringComparison.OrdinalIgnoreCase)) {
                    result.ToContains = value;
                } else if (string.Equals(key, "subject", StringComparison.OrdinalIgnoreCase)) {
                    result.Subject = value;
                } else if (string.Equals(key, "since", StringComparison.OrdinalIgnoreCase)) {
                    if (DateTime.TryParse(value, out var sd)) result.Since = sd;
                } else if (string.Equals(key, "before", StringComparison.OrdinalIgnoreCase)) {
                    if (DateTime.TryParse(value, out var bd)) result.Before = bd;
                } else if (string.Equals(key, "priority", StringComparison.OrdinalIgnoreCase)) {
                    if (Enum.TryParse(value, true, out MessagePriority pr)) result.Priority = pr;
                } else if (string.Equals(key, "has", StringComparison.OrdinalIgnoreCase)) {
                    if (value.Equals("attachment", StringComparison.OrdinalIgnoreCase) || value.Equals("attachments", StringComparison.OrdinalIgnoreCase)) result.HasAttachment = true;
                } else if (string.Equals(key, "body", StringComparison.OrdinalIgnoreCase)) {
                    result.BodyContains = value;
                } else {
                    result.AdditionalQueries.Add(SearchQuery.MessageContains(token));
                }
            } else {
                var text = Unquote(token);
                result.AdditionalQueries.Add(SearchQuery.MessageContains(text));
            }
        }
        return result;
    }

    private static DateTime? NormalizeToUtc(DateTime? value) {
        if (!value.HasValue) {
            return null;
        }

        var dt = value.Value;
        if (dt.Kind == DateTimeKind.Unspecified) {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        return dt.ToUniversalTime();
    }

    private static IEnumerable<string> SplitTokens(string input) {
        var list = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var ch in input) {
            if (ch == '"') {
                inQuotes = !inQuotes;
            } else if (char.IsWhiteSpace(ch) && !inQuotes) {
                if (current.Length > 0) {
                    list.Add(current.ToString());
                    current.Clear();
                }
            } else {
                current.Append(ch);
            }
        }
        if (current.Length > 0) list.Add(current.ToString());
        return list;
    }

    private static string Unquote(string value) {
        if (value.Length > 1 &&
            value.StartsWith("\"", StringComparison.Ordinal) &&
            value.EndsWith("\"", StringComparison.Ordinal))
            return value.Substring(1, value.Length - 2);
        return value;
    }
}
