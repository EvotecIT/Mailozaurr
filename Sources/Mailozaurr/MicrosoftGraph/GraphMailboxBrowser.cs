using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// High-level delegated-token mailbox browsing helpers built on top of <see cref="GraphApiClient"/>.
/// </summary>
public sealed partial class GraphMailboxBrowser {
    private const string SummarySelect = "id,subject,receivedDateTime,from,toRecipients,internetMessageId,hasAttachments,isRead,flag,conversationId";
    // Must be a multiple of 320 KiB (except last chunk).
    private const int LargeAttachmentChunkSize = 327_680 * 32; // 10 MiB
    private readonly GraphApiClient _graph;

    /// <summary>
    /// Maximum MIME payload size used by <see cref="GetMessageContentAsync"/> when no explicit limit is provided.
    /// </summary>
    public const int DefaultMaxMimeBytes = 25 * 1024 * 1024;

    /// <summary>
    /// Maximum MIME payload size used by <see cref="GetThreadingMetadataAsync"/> when no explicit limit is provided.
    /// </summary>
    public const int DefaultThreadingMetadataMaxMimeBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMailboxBrowser"/> class.
    /// </summary>
    /// <param name="graph">Graph API client.</param>
    public GraphMailboxBrowser(GraphApiClient graph) {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }





}