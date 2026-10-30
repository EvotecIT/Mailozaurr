# Mailozaurr working rules

SMTP transport policy belongs in Mailozaurr.Internet. Profile and PowerShell routes
share that implementation. Use loopback SMTP fixtures for validation; never send real mail.

Before adding or changing tests, read the shared authoring gate at
`$CODEX_HOME/skills/test-audit/SKILL.md#authoring-gate` and proof guidance at
`$CODEX_HOME/skills/contract-focused-tests/SKILL.md` (default CODEX_HOME: `~/.codex`).
For an explicitly requested pruning campaign, read `$CODEX_HOME/skills/test-audit/CAMPAIGN.md`.
