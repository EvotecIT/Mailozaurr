---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MimeMessageContent
## SYNOPSIS
Retrieves text and HTML bodies from a MIME message.

## SYNTAX
### __AllParameterSets
```powershell
Get-MimeMessageContent -InputObject <Object> [<CommonParameters>]
```

## DESCRIPTION
Retrieves text and HTML bodies from a MIME message.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MimeMessageContent -InputObject 'Value'
```


## PARAMETERS

### -InputObject
Message to extract content from.

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

- `Mailozaurr.MimeMessageContent`

## RELATED LINKS

- None
