---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Clear-SmtpConnectionPool
## SYNOPSIS
Clears all cached SMTP connections used for connection pooling.

The Clear-SmtpConnectionPool cmdlet removes any
pooled SMTP connections maintained by the Smtp class. Use this
when you want to force new connections, for example after changing credentials
or server settings.

## SYNTAX
### __AllParameterSets
```powershell
Clear-SmtpConnectionPool [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Clears all cached SMTP connections used for connection pooling.

The Clear-SmtpConnectionPool cmdlet removes any
pooled SMTP connections maintained by the Smtp class. Use this
when you want to force new connections, for example after changing credentials
or server settings.

## EXAMPLES

### EXAMPLE 1
```powershell
Clear-SmtpConnectionPool
```


## PARAMETERS

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- None
