---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-POP3MessageAttachment
## SYNOPSIS
Removes attachments from a POP3 MimeMessage instance.

## SYNTAX
### __AllParameterSets
```powershell
Remove-POP3MessageAttachment -InputObject <Object> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes attachments from a POP3 MimeMessage instance.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-POP3MessageAttachment -InputObject 'Value'
```


## PARAMETERS

### -InputObject
MIME message to process.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: Message
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

- `System.Object`

## OUTPUTS

- `MimeKit.MimeMessage`

## RELATED LINKS

- None
