# Mailozaurr Platform Architecture

This document defines how Mailozaurr should evolve across its library, PowerShell, CLI, MCP, and GUI surfaces.

The main goal is reuse. If a new capability can be shared by more than one surface, it should not be implemented only in a wrapper. It should live in the reusable layer and be consumed by wrappers.

## Goals

- Keep one canonical implementation of reusable mail behavior.
- Avoid duplicating business logic across PowerShell, CLI, MCP, GUI, and ad hoc examples.
- Support provider differences without forcing a fake one-size-fits-all abstraction.
- Make new work easier to place correctly.
- Keep public surfaces consistent in naming, behavior, and safety rules.

## Product Surfaces

Mailozaurr should be treated as one platform with multiple front doors:

- `Mailozaurr`: reusable .NET library and provider engine
- `Mailozaurr.PowerShell`: PowerShell adapter
- `mailozaurr`: cross-platform CLI
- `mailozaurr mcp serve`: MCP server mode
- Desktop/GUI apps built on the same core concepts

These are not separate products with separate logic. They are adapters over shared capabilities.

## Target Layering

The preferred long-term structure is:

- `Mailozaurr`
- `Mailozaurr.Application`
- `Mailozaurr.PowerShell`
- `Mailozaurr.Cli`, including the MCP host
- Desktop/GUI app projects

`Mailozaurr.Application` and `Mailozaurr.Cli` have package definitions and are built by the shared release pipeline, but they are not public packages yet. Until they are explicitly published, consume Application through a project reference and run the CLI from source or from a locally built PowerForge artifact. Public installation instructions should only be added after those artifacts exist on the public feed.

### Layer Responsibilities

`Mailozaurr`

- Provider and protocol implementations
- SMTP, IMAP, POP3, Graph, Gmail, SendGrid, Mailgun, SES support
- Authentication primitives and provider clients
- Message parsing, conversion, attachments, transport rules
- Reusable send, search, fetch, save, move, mark, delete logic
- Reusable queue and pending-message primitives
- Provider-specific models and helpers

`Mailozaurr.Application`

- Cross-surface workflows and orchestration
- Profile store and profile validation
- Secret and token orchestration
- Session management
- Capability discovery
- Normalized DTOs used by CLI, MCP, GUI, and optionally PowerShell
- Safe send, queue, draft, delete, and move workflows
- Audit and operation result models

`Mailozaurr.PowerShell`

- Cmdlet binding and parameter mapping
- PowerShell pipeline and `ShouldProcess` integration
- Formatting and help metadata
- PowerShell-only compatibility behaviors

`Mailozaurr.Cli`

- Command parsing
- Human-readable output
- JSON output
- Exit codes
- MCP transport and tool registration
- Tool schema definitions
- Mapping tool calls to application services

GUI projects

- View models, UI state, platform integration
- Human-first flows for accounts, mailbox browsing, drafts, and queue review

## Reuse Rules

These rules should be used when deciding where new work belongs.

### Rule 1

If logic is reusable by more than one surface, it should not be implemented only in PowerShell, CLI, MCP, or GUI code.

It should be placed in:

- `Mailozaurr` when it is protocol/provider behavior or reusable low-level mail functionality
- `Mailozaurr.Application` when it is a cross-surface workflow, orchestration rule, normalized DTO, or policy

### Rule 2

Wrappers should adapt, not own business logic.

PowerShell, CLI, MCP, and GUI layers should translate user intent into calls to shared services. They should not each reinvent send/search/profile behavior.

### Rule 3

Provider-neutral concepts should be shared. Provider-specific concepts should remain explicit.

Do not force every provider to look identical. Instead:

- share common operations such as search, get message, save attachment, draft, queue, send, mark, move, delete
- keep provider-specific operations explicit, such as Graph inbox rules, Graph events, Gmail threads, Gmail labels, IMAP IDLE behavior, and POP3 limitations

### Rule 4

A wrapper-only implementation is acceptable only when the behavior is genuinely wrapper-specific.

Examples:

- PowerShell parameter sets and pipeline binding belong in `Mailozaurr.PowerShell`
- CLI help text and shell-friendly formatting belong in `Mailozaurr.Cli`
- MCP tool schemas belong in `Mailozaurr.Cli/Mcp`
- GUI dialogs, views, and local UX behavior belong in the GUI project

## Feature Placement Guide

Use this checklist when adding a new feature.

Place it in `Mailozaurr` if it is:

- a provider client enhancement
- a reusable transport or mailbox operation
- message parsing, attachment, MIME, cryptography, or auth logic
- reusable folder/message/provider helpers
- reusable pending-message or send pipeline logic

Place it in `Mailozaurr.Application` if it is:

- a profile concept shared by multiple front ends
- a cross-provider workflow
- a capability model
- a normalized DTO for message summaries/details
- a queue or draft workflow shared by CLI, MCP, GUI, or PowerShell
- a safety policy for destructive operations or sending
- an operation result or audit event that more than one surface should use

Place it in `Mailozaurr.PowerShell` if it is:

- cmdlet syntax
- PowerShell-specific validation and parameter shaping
- `ShouldProcess`, verbose output, pipeline behavior, and formatting

Place it in `Mailozaurr.Cli` if it is:

- command syntax
- argument parsing
- console output formatting
- shell completion and exit code concerns
- MCP server bootstrapping
- tool declaration and transport behavior
- mapping between tool requests and application services

Place it in the GUI project if it is:

- view logic
- window/dialog composition
- UI state management
- platform-specific desktop integration

## Canonical Shared Concepts

The following concepts should use consistent naming across all surfaces:

- `Profile`
- `ProfileCapabilities`
- `FolderRef`
- `MailboxRef`
- `MessageSummary`
- `MessageDetail`
- `AttachmentSummary`
- `DraftMessage`
- `QueuedMessage`
- `SendResult`
- `OperationResult`

The same concept should not have different names in different surfaces unless there is a compelling reason.

## Provider Capability Model

Mailozaurr should be designed around capabilities, not around the assumption that every provider supports the same things.

### Common Capability Areas

- read/search messages
- list folders or mailbox containers
- get message details
- save attachments
- mark/move/delete messages
- send messages
- wait/listen for new messages

### Provider Notes

- IMAP: strong mailbox support, folders, flags, move, search, and wait/idle patterns
- POP3: limited mailbox model, fetch/search/delete style workflows, no real folder story
- Graph: rich mailbox and send support, plus rules, events, and permissions
- Gmail API: mailbox and send support with Gmail-specific thread and label behavior
- SMTP: send only, not mailbox browsing
- SendGrid/Mailgun/SES: send only, provider-specific sending features

The shared layers should expose common operations where they exist and explicit provider extensions where they do not.

## Profiles and Sessions

Future work should prefer explicit profiles over implicit global sessions.

Profiles should store:

- provider type
- display name
- server or tenant settings
- auth mode
- sender defaults where relevant
- mailbox defaults where relevant
- non-secret metadata in config
- secrets in secure OS-backed storage where possible

Wrappers should not rely on hidden global state when a profile or explicit handle can be used instead.

## Safety Rules

Safety behavior should be shared, not reimplemented separately in each surface.

Preferred defaults:

- drafts before send when practical
- immediate delivery for explicit send requests
- queue-on-failure only when the caller asks for it, with queue processing kept as a separate workflow
- explicit confirmation for destructive actions
- provider-limit validation before send
- dry-run support where possible
- reusable audit and logging models

## CLI and MCP Design Direction

The CLI and MCP server should be thin adapters over shared services.

The recommended shape is:

- CLI for human and automation use
- MCP as a structured tool surface over the same workflows
- one executable host that runs both normal CLI commands and `mcp serve`

This avoids duplicating logic and gives one headless contract for the ecosystem.

## Skills

Skills should be treated as reusable agent guidance, not as a replacement for shared Mailozaurr capabilities.

The preferred split is:

- `MCP` exposes executable tools over shared Mailozaurr services
- `skills` teach agents how to use those tools safely and consistently

Examples of skill responsibilities:

- when to prefer drafts over immediate send
- when to opt into queue-on-failure or process the persistent queue
- how to summarize before delete/move
- how to select compact versus rich read surfaces

Examples of non-skill responsibilities:

- mailbox search logic
- message retrieval
- attachment saving
- queue processing
- provider auth/session behavior

Those belong in shared code, not only in prompt guidance.

## HTML Body Projection Roadmap

Mailozaurr should eventually support reusable HTML body projection for non-PowerShell headless surfaces, especially CLI and MCP.

The recommended split is:

- keep raw provider body retrieval in `Mailozaurr`
- place body projection abstractions and policies in `Mailozaurr.Application`
- keep PowerShell dependency-light by default
- document PowerShell recipes for richer HTML handling instead of forcing heavy dependencies into the module

### Recommended integration model

- `CLI` / `MCP`
  Add optional shared HTML-to-Markdown projection via `OfficeIMO.Markdown.Html`
- `PowerShell`
  Keep raw body access in the Mailozaurr module
  Show examples using `PSWriteOffice` / `OfficeIMO.Markdown.Html` for Markdown projection
  Show examples using `PSParseHTML` for DOM-style extraction and structured data recovery

### Placement rule

If HTML-to-Markdown projection becomes reusable across CLI, MCP, GUI, or plain C# consumers, the abstraction belongs in shared Mailozaurr layers.

If a PowerShell workflow only demonstrates how to pipe a retrieved HTML body into another module, that belongs in documentation/examples, not in the core Mailozaurr PowerShell adapter by default.

## Migration Guidance

Not every existing area must be refactored immediately.

When touching an existing feature:

1. Keep behavior working.
2. Extract reusable logic into `Mailozaurr` or `Mailozaurr.Application` when the change naturally allows it.
3. Leave only surface-specific adaptation in PowerShell, CLI, MCP, or GUI code.

Incremental improvement is preferred over a disruptive rewrite.

## Decision Rule for New Work

Before adding code, ask:

1. Is this reusable outside the current wrapper?
2. Is it protocol/provider logic or workflow/orchestration logic?
3. Will CLI, MCP, GUI, PowerShell, or plain C# users eventually want the same behavior?

If the answer to reuse is yes, the default destination should be a shared layer, not the wrapper.

## Working Principle

Mailozaurr should have one core behavior model with many adapters, not many implementations with similar names.

That principle should guide future CLI, MCP, GUI, PowerShell, and library work.
