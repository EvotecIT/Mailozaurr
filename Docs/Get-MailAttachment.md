---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailAttachment
## SYNOPSIS
Lists or filters a message's attachment descriptions.

Includes regular files, inline resources and hidden attachments by default. The command reads metadata already present on the message and does not reopen the source or save bytes.

## SYNTAX
### __AllParameterSets
```powershell
Get-MailAttachment -InputObject <Object> [-FileName <string[]>] [-ContentType <string[]>] [-InlineOnly] [<CommonParameters>]
```

## DESCRIPTION
Lists or filters a message's attachment descriptions.

Includes regular files, inline resources and hidden attachments by default. The command reads metadata already present on the message and does not reopen the source or save bytes.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailMessage -Path './Mail/invoice.eml' | Get-MailAttachment -InlineOnly -ContentType 'image/*'
```


## PARAMETERS

### -ContentType
Media-type wildcard patterns, such as image/*.

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

### -FileName
Filename wildcard patterns; a match against any pattern is sufficient.

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

### -InlineOnly
Returns only attachments marked inline.

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

### -InputObject
Message or attachment description from Get-MailMessage or Get-MailAttachment.

```yaml
Type: Object
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

- `System.Object`

## OUTPUTS

- `OfficeIMO.Email.EmailMessageAttachment`

## RELATED LINKS

- None
