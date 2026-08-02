---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Send-EmailPendingMessage
## SYNOPSIS
Sends pending messages stored in a file-based repository.

## SYNTAX
### __AllParameterSets
```powershell
Send-EmailPendingMessage -PendingMessagesPath <string> [-Provider <EmailProvider>] [-MessageId <string[]>] [-ProcessAll] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Sends pending messages stored in a file-based repository.

## EXAMPLES

### EXAMPLE 1
```powershell
Send-EmailPendingMessage -PendingMessagesPath 'C:\Path'
```


## PARAMETERS

### -MessageId
Identifiers of specific messages that should be retried immediately.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PendingMessagesPath
Directory containing pending message log file.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: PendingPath
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProcessAll
Processes all messages regardless of their scheduled retry time.

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

### -Provider
Filters queued messages to the specified provider when supplied.

```yaml
Type: EmailProvider
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: None, SendGrid, Mailgun, SES, Gmail, Graph

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

- `None`

## RELATED LINKS

- None
