---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-SmtpConnectionPool
## SYNOPSIS
Retrieves information about the SMTP connection pool.

The Get-SmtpConnectionPool cmdlet returns a snapshot of
pooled SMTP connections or, when used with -Watch, continuously emits
updates as the pool changes. An optional -Action script block can be
executed for each update.

## SYNTAX
### __AllParameterSets
```powershell
Get-SmtpConnectionPool [-Watch] [-Action <scriptblock>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves information about the SMTP connection pool.

The Get-SmtpConnectionPool cmdlet returns a snapshot of
pooled SMTP connections or, when used with -Watch, continuously emits
updates as the pool changes. An optional -Action script block can be
executed for each update.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-SmtpConnectionPool -Action { }
```


## PARAMETERS

### -Action
Script block executed for each snapshot when watching.

```yaml
Type: ScriptBlock
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Watch
Continuously watch the pool for changes.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
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

- `Mailozaurr.SmtpConnectionPoolSnapshot`

## RELATED LINKS

- None
