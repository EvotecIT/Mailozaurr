---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Watch-SmtpConnectionPool
## SYNOPSIS
Subscribes to SMTP connection pool updates.

The Watch-SmtpConnectionPool cmdlet registers a handler
that executes a provided script block whenever the SMTP connection pool
changes. The handler is returned so it can be removed when no longer
needed.

## SYNTAX
### __AllParameterSets
```powershell
Watch-SmtpConnectionPool -Action <scriptblock> [<CommonParameters>]
```

## DESCRIPTION
Subscribes to SMTP connection pool updates.

The Watch-SmtpConnectionPool cmdlet registers a handler
that executes a provided script block whenever the SMTP connection pool
changes. The handler is returned so it can be removed when no longer
needed.

## EXAMPLES

### EXAMPLE 1
```powershell
Watch-SmtpConnectionPool -Action { }
```


## PARAMETERS

### -Action
Script block executed for each snapshot.

```yaml
Type: ScriptBlock
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- ``System.Action`1[[System.Int32, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]``

## RELATED LINKS

- None
