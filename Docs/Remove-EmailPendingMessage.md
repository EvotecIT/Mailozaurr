---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-EmailPendingMessage
## SYNOPSIS
Removes pending messages from a file based repository.

## SYNTAX
### __AllParameterSets
```powershell
Remove-EmailPendingMessage -MessageId <string> -PendingMessagesPath <string> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes pending messages from a file based repository.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-EmailPendingMessage -MessageId 'Value' -PendingMessagesPath 'C:\Path'
```


## PARAMETERS

### -MessageId
Identifier of the message to remove.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- None
