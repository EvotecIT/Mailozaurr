---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-GraphMessageAttachment
## SYNOPSIS
Removes attachments from a GraphMessage instance.

## SYNTAX
### __AllParameterSets
```powershell
Remove-GraphMessageAttachment -Message <GraphMessage> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes attachments from a GraphMessage instance.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-GraphMessageAttachment -Message 'Value'
```


## PARAMETERS

### -Message
Graph message to process.

```yaml
Type: GraphMessage
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.GraphMessage`

## OUTPUTS

- `Mailozaurr.GraphMessage`

## RELATED LINKS

- None
