---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Close-MailData
## SYNOPSIS
Closes an imported email-data artifact and its owned resources.

Disposes the result returned by Import-MailData, closing PST/OST/OLM/Mbox/mailbox-directory or OAB sessions and releasing file-backed email attachment content.

## SYNTAX
### __AllParameterSets
```powershell
Close-MailData [-InputObject] <EmailDataOpenResult> [<CommonParameters>]
```

## DESCRIPTION
Closes an imported email-data artifact and its owned resources.

Disposes the result returned by Import-MailData, closing PST/OST/OLM/Mbox/mailbox-directory or OAB sessions and releasing file-backed email attachment content.

## EXAMPLES

### EXAMPLE 1
```powershell
Close-MailData -InputObject 'Value'
```


## PARAMETERS

### -InputObject
The owner result returned by Import-MailData.

```yaml
Type: EmailDataOpenResult
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `OfficeIMO.Email.Data.EmailDataOpenResult`

## OUTPUTS

- `None`

## RELATED LINKS

- None
