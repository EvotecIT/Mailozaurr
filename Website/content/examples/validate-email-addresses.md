---
title: "Validate email addresses"
description: "Use Mailozaurr to validate email address strings from parameters or pipeline input."
layout: docs
---

This pattern is useful when an automation accepts address lists from files, forms, or another system.

It comes from the source example at `Examples/Example-ValidateEmail.ps1`.

## When to use this pattern

- You accept email addresses from user input.
- You import recipients from CSV or another system.
- You want invalid values to be visible before sending.

## Example

```powershell
Import-Module .\Mailozaurr.psd1 -Force

$addresses = 'admin@example.com', 'broken-address', 'helpdesk@example.org'
$addresses | Test-EmailAddress -Verbose | Format-Table
```

## What this demonstrates

- validating parameter and pipeline input
- making bad addresses visible early
- keeping send workflows separate from validation

## Source

- [Example-ValidateEmail.ps1](https://github.com/EvotecIT/Mailozaurr/blob/v2-speedygonzales/Examples/Example-ValidateEmail.ps1)

