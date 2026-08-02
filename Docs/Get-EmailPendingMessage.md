---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-EmailPendingMessage
## SYNOPSIS
Retrieves pending email messages from a file based repository.

## SYNTAX
### __AllParameterSets
```powershell
Get-EmailPendingMessage -PendingMessagesPath <string> [<CommonParameters>]
```

## DESCRIPTION
Retrieves pending email messages from a file based repository.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EmailPendingMessage -PendingMessagesPath 'C:\Path'
```


## PARAMETERS

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

- `Mailozaurr.PendingMessageRecord`

## RELATED LINKS

- None
