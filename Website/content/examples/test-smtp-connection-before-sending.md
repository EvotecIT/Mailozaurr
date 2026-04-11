---
title: "Test SMTP before sending"
description: "Use Mailozaurr to test SMTP capabilities and choose a safer send pattern."
layout: docs
---

This pattern is useful before adding SMTP sending to an operational script.

It comes from the source example at `Examples/Example-TestSmtpConnection.ps1`.

## When to use this pattern

- You need to confirm SMTP connectivity before sending mail.
- You want to decide whether connection pooling is safe.
- You are adding a WhatIf-protected send path to a script.

## Example

```powershell
Import-Module .\Mailozaurr.psd1 -Force

$info = Test-SmtpConnection -Server 'smtp.example.com' -Port 587

$poolSettings = if ($info.Persistent) {
    @{ UseConnectionPool = $true; ConnectionPoolSize = 2 }
} else {
    @{}
}

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Server 'smtp.example.com' @poolSettings -WhatIf
```

## What this demonstrates

- testing the transport before sending
- using SMTP capability data to shape the send path
- keeping the example safe with WhatIf

## Source

- [Example-TestSmtpConnection.ps1](https://github.com/EvotecIT/Mailozaurr/blob/v2-speedygonzales/Examples/Example-TestSmtpConnection.ps1)

