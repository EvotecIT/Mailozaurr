# Library roadmap

Mailozaurr keeps transport and mailbox behavior in the reusable .NET libraries, with PowerShell, CLI, and MCP consuming those contracts. This roadmap prioritizes work that reduces transferred data, memory use, or recovery ambiguity. Each item needs a concrete consumer and measurable validation before implementation.

## Near-term performance work

- [ ] Retrieve IMAP summaries before message bodies, then fetch only the MIME parts a caller requests. Validate large messages with attachments and verify network bytes as well as allocations. Existing body projection limits must not be described as network download limits.
- [ ] Store large queued MIME payloads as immutable files with small metadata records. Preserve lease fencing, acceptance markers, credential protection, atomic writes, and recovery after interruption. Provide migration for existing logs and measure processing and compaction with realistic attachments.
- [ ] Establish repeatable benchmarks through the shared PowerForge benchmark tooling for SMTP pool reuse, mailbox summaries, upload sessions, and queue processing. Record runtime, affinity, allocations, transferred bytes, and rotated comparisons before changing defaults.

## Diagnostics and incremental mailbox workflows

- [ ] Add optional `ActivitySource` and `Meter` instrumentation for transport duration, retries, throttling, queue age, lease loss, and acknowledgement failures. Keep tokens, passwords, recipient addresses, message bodies, and attachment names out of default telemetry.
- [ ] Expose resumable mailbox synchronization through the existing Graph delta, Gmail history, and JMAP changes capabilities. Bind checkpoints to provider identity and mailbox scope; define restart behavior when a cursor expires.
- [ ] Add asynchronous streaming APIs for pages and message content where the provider supports them. Make cancellation, ordering, partial failures, stream ownership, and maximum transferred bytes explicit. Retain convenient buffered APIs for small workloads.

## Features to add when consumers need them

- [ ] Add controlled bulk-send scheduling over the shared queue and transport engines. Validate provider-specific rate limits, fair account scheduling, shutdown recovery, and acceptance handling before promising throughput targets.
- [ ] Extend JMAP mutation workflows for a concrete supported server and consumer. Keep capability discovery and partial-result reporting in the JMAP library, and qualify behavior against that server before adding surface-specific wrappers.

Current performance controls include `GmailMailboxBrowser.SummaryDownloadConcurrency` (default `4`; set `1` for sequential summary requests) and the process-wide `MicrosoftGraphUtils.MaxConcurrentRequests`. Gmail summary loading preserves result order, skips messages that disappeared with a 404, and propagates other provider failures and cancellation. Reducing Graph concurrency allows active requests to finish before admitting more work. Graph upload paths share the chunk uploader and reuse one working buffer per attachment. Async IMAP receive helpers use `ImapClientFolderCache.GetCachedFolderAsync` for folder lookup and opening.
