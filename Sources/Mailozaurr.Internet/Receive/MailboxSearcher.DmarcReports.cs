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
        CancellationToken cancellationToken = default) => await SearchDmarcReportsAsync(
            client,
            DmarcReportInspectionOptions.FromLegacyLimit(maxUncompressedSize),
            folder,
            since,
            before,
            domain,
            maxResults,
            parallelDownloadLimit,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Searches for DMARC aggregate reports using explicit attachment inspection limits.</summary>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        ImapClient client,
        DmarcReportInspectionOptions inspectionOptions,
        string? folder = null,
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        if (inspectionOptions == null) throw new ArgumentNullException(nameof(inspectionOptions));
        var inspectionPolicy = inspectionOptions.CreatePolicy();
        var inspectionBudget = new SharedReadBudget(inspectionPolicy.MaxTotalUncompressedBytes);
        var mimeBudget = new DmarcMimeDownloadBudget(
            inspectionPolicy.MaxMimeBytesPerMessage,
            inspectionPolicy.MaxTotalMimeBytes);
        var mailFolder = client.GetCachedFolder(folder, FolderAccess.ReadOnly);
        var search = BuildDmarcReportSearchQuery(since, before, domain);
        var matchingUids = (await mailFolder.SearchAsync(search, cancellationToken).ConfigureAwait(false))
            .ToArray();
        var uids = matchingUids
            .Skip(Math.Max(0, matchingUids.Length - inspectionPolicy.MaxMessagesScanned))
            .Reverse()
            .ToArray();
        var results = new List<DmarcReport>();
        if (parallelDownloadLimit <= 1) {
            foreach (var uid in uids) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await mailFolder.GetMessageAsync(
                    uid,
                    cancellationToken,
                    mimeBudget.CreateTransferProgress()).ConfigureAwait(false);
                var reports = FilterDmarcReports(
                    new[] { msg }, since, before, domain, inspectionPolicy, inspectionBudget, cancellationToken);
                if (reports.Count > 0) {
                    foreach (var r in reports) {
                        if (maxResults > 0 && results.Count >= maxResults) break;
                        results.Add(r);
                    }
                    if (maxResults > 0 && results.Count >= maxResults) break;
                }
            }
        } else {
            var uidArray = uids;
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
                            msg = await mailFolder.GetMessageAsync(
                                uidArray[current],
                                cts.Token,
                                mimeBudget.CreateTransferProgress()).ConfigureAwait(false);
                        } catch (OperationCanceledException) {
                            return;
                        }
                        var reports = FilterDmarcReports(
                            new[] { msg }, since, before, domain, inspectionPolicy, inspectionBudget, cts.Token);
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
            cancellationToken.ThrowIfCancellationRequested();
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
        CancellationToken cancellationToken = default) => await SearchDmarcReportsAsync(
            client,
            DmarcReportInspectionOptions.FromLegacyLimit(maxUncompressedSize),
            since,
            before,
            domain,
            maxResults,
            parallelDownloadLimit,
            cancellationToken).ConfigureAwait(false);

    /// <summary>Searches for DMARC aggregate reports using explicit attachment inspection limits.</summary>
    public static async Task<IList<DmarcReport>> SearchDmarcReportsAsync(
        Pop3Client client,
        DmarcReportInspectionOptions inspectionOptions,
        DateTime? since = null,
        DateTime? before = null,
        string? domain = null,
        int maxResults = 0,
        int parallelDownloadLimit = 4,
        CancellationToken cancellationToken = default) {
        if (inspectionOptions == null) throw new ArgumentNullException(nameof(inspectionOptions));
        var inspectionPolicy = inspectionOptions.CreatePolicy();
        var inspectionBudget = new SharedReadBudget(inspectionPolicy.MaxTotalUncompressedBytes);
        var mimeBudget = new DmarcMimeDownloadBudget(
            inspectionPolicy.MaxMimeBytesPerMessage,
            inspectionPolicy.MaxTotalMimeBytes);
        int messagesToScan = Math.Min(client.Count, inspectionPolicy.MaxMessagesScanned);
        int firstMessageIndex = client.Count - messagesToScan;
        var results = new List<DmarcReport>();
        if (parallelDownloadLimit <= 1) {
            for (int idx = client.Count - 1; idx >= firstMessageIndex; idx--) {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = await client.GetMessageAsync(
                    idx,
                    cancellationToken,
                    mimeBudget.CreateTransferProgress()).ConfigureAwait(false);
                var reports = FilterDmarcReports(
                    new[] { msg }, since, before, domain, inspectionPolicy, inspectionBudget, cancellationToken);
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
            var workers = new Task[Math.Min(parallelDownloadLimit, messagesToScan)];
            int next = client.Count - 1;
            int resultCount = 0;
            var gate = new object();
            for (int i = 0; i < workers.Length; i++) {
                workers[i] = Task.Run(async () => {
                    while (true) {
                        int current;
                        lock (gate) {
                            if (cts.IsCancellationRequested || next < firstMessageIndex || (maxResults > 0 && resultCount >= maxResults)) return;
                            current = next--;
                        }
                        MimeMessage msg;
                        try {
                            msg = await client.GetMessageAsync(
                                current,
                                cts.Token,
                                mimeBudget.CreateTransferProgress()).ConfigureAwait(false);
                        } catch (OperationCanceledException) {
                            return;
                        }
                        var reports = FilterDmarcReports(
                            new[] { msg }, since, before, domain, inspectionPolicy, inspectionBudget, cts.Token);
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
            cancellationToken.ThrowIfCancellationRequested();
        }

        return maxResults > 0 && results.Count > maxResults ? results.GetRange(0, maxResults) : results;
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
        var policy = DmarcReportInspectionOptions.FromLegacyLimit(maxUncompressedSize).CreatePolicy();
        return FilterDmarcReports(
            messages,
            since,
            before,
            domain,
            policy,
            new SharedReadBudget(policy.MaxTotalUncompressedBytes));
    }

    internal static IList<DmarcReport> FilterDmarcReports(
        IEnumerable<MimeMessage> messages,
        DateTime? since,
        DateTime? before,
        string? domain,
        DmarcReportInspectionPolicy inspectionPolicy,
        SharedReadBudget inspectionBudget,
        CancellationToken cancellationToken = default) {
        var results = new List<DmarcReport>();
        var sinceUtc = NormalizeToUtc(since);
        var beforeUtc = NormalizeToUtc(before);
        foreach (var message in messages) {
            cancellationToken.ThrowIfCancellationRequested();
            var msgDate = message.Date.UtcDateTime;
            if (sinceUtc.HasValue && msgDate < sinceUtc.Value) continue;
            if (beforeUtc.HasValue && msgDate > beforeUtc.Value) continue;
            var report = new DmarcReport {
                From = message.From.Mailboxes.FirstOrDefault()?.Address,
                Subject = message.Subject,
                Date = message.Date
            };
            var domainMatched = string.IsNullOrWhiteSpace(domain);
            int inspectedAttachments = 0;
            foreach (var att in message.Attachments) {
                if (IsDmarcAttachment(att) && att is MimePart part) {
                    if (inspectedAttachments >= inspectionPolicy.MaxAttachmentsPerMessage) {
                        LoggingMessages.Logger.WriteError(
                            "DMARC message exceeds the attachment limit of {0}",
                            inspectionPolicy.MaxAttachmentsPerMessage);
                        break;
                    }
                    inspectedAttachments++;
                    if (!InspectDmarcAttachment(
                            part,
                            domain,
                            inspectionPolicy,
                            inspectionBudget,
                            cancellationToken,
                            out bool attachmentDomainMatched)) continue;
                    if (!domainMatched && !attachmentDomainMatched) continue;
                    if (part.Content == null) {
                        continue;
                    }

                    var stream = part.Content.Open();
                    report.Attachments.Add(new DmarcReportAttachment(part.FileName ?? "report.zip", stream));
                    if (!domainMatched) domainMatched = attachmentDomainMatched;
                }
            }
            if (report.Attachments.Count > 0 && domainMatched) results.Add(report);
        }
        return results;
    }

    private static bool InspectDmarcAttachment(
        MimePart part,
        string? domain,
        DmarcReportInspectionPolicy inspectionPolicy,
        SharedReadBudget inspectionBudget,
        CancellationToken cancellationToken,
        out bool domainMatched) {
        domainMatched = !string.IsNullOrWhiteSpace(domain)
            && part.FileName?.IndexOf(domain!, StringComparison.OrdinalIgnoreCase) >= 0;
        if (part.Content == null) {
            return false;
        }

        try {
            using var stream = part.Content.Open();
            var attachmentBudget = new SharedReadBudget(inspectionPolicy.MaxUncompressedBytesPerAttachment);
            var name = part.FileName ?? string.Empty;
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                if (zip.Entries.Count > inspectionPolicy.MaxArchiveEntriesPerAttachment) {
                    LoggingMessages.Logger.WriteError(
                        "DMARC ZIP attachment {0} exceeds the entry limit of {1}",
                        name,
                        inspectionPolicy.MaxArchiveEntriesPerAttachment);
                    return false;
                }

                long declaredTotal = 0;
                foreach (var entry in zip.Entries) {
                    if (entry.Length < 0 || entry.Length > inspectionPolicy.MaxUncompressedBytesPerAttachment
                        || declaredTotal > inspectionPolicy.MaxUncompressedBytesPerAttachment - entry.Length) {
                        LoggingMessages.Logger.WriteError(
                            "DMARC ZIP attachment {0} exceeds the expanded size limit of {1}",
                            name,
                            inspectionPolicy.MaxUncompressedBytesPerAttachment);
                        return false;
                    }
                    declaredTotal += entry.Length;
                }

                foreach (var entry in zip.Entries) {
                    using var entryStream = entry.Open();
                    if (string.IsNullOrWhiteSpace(domain) || domainMatched) {
                        DrainStream(entryStream, attachmentBudget, inspectionBudget, cancellationToken);
                    } else if (XmlStreamContainsDomain(
                                   entryStream, domain!, attachmentBudget, inspectionBudget, cancellationToken)) {
                        domainMatched = true;
                    }
                }
            } else if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) {
                using var gz = new GZipStream(stream, CompressionMode.Decompress);
                if (string.IsNullOrWhiteSpace(domain) || domainMatched) {
                    DrainStream(gz, attachmentBudget, inspectionBudget, cancellationToken);
                } else {
                    domainMatched = XmlStreamContainsDomain(
                        gz, domain!, attachmentBudget, inspectionBudget, cancellationToken);
                }
            } else {
                if (string.IsNullOrWhiteSpace(domain) || domainMatched) {
                    DrainStream(stream, attachmentBudget, inspectionBudget, cancellationToken);
                } else {
                    domainMatched = XmlStreamContainsDomain(
                        stream, domain!, attachmentBudget, inspectionBudget, cancellationToken);
                }
            }
            return string.IsNullOrWhiteSpace(domain) || domainMatched;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteError("Failed to process attachment {0}: {1}", part.FileName ?? string.Empty, ex.Message);
        }
        return false;
    }

    private static bool XmlStreamContainsDomain(
        Stream stream,
        string domain,
        SharedReadBudget attachmentBudget,
        SharedReadBudget inspectionBudget,
        CancellationToken cancellationToken) {
        var settings = new System.Xml.XmlReaderSettings {
            IgnoreComments = true,
            IgnoreWhitespace = true,
            CloseInput = false,
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var boundedStream = new SharedBudgetReadStream(
            stream, attachmentBudget, inspectionBudget, cancellationToken);
        using var reader = System.Xml.XmlReader.Create(boundedStream, settings);
        bool matched = false;
        while (reader.Read()) {
            if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.LocalName.Equals("domain", StringComparison.OrdinalIgnoreCase)) {
                var value = reader.ReadElementContentAsString();
                if (value.Equals(domain, StringComparison.OrdinalIgnoreCase)) matched = true;
            }
        }
        return matched;
    }

    private static void DrainStream(
        Stream stream,
        SharedReadBudget attachmentBudget,
        SharedReadBudget inspectionBudget,
        CancellationToken cancellationToken) {
        using var boundedStream = new SharedBudgetReadStream(
            stream, attachmentBudget, inspectionBudget, cancellationToken);
        var buffer = new byte[81920];
        while (boundedStream.Read(buffer, 0, buffer.Length) > 0) {
        }
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
}
