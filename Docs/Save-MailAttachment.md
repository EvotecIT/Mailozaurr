---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-MailAttachment
## SYNOPSIS
Saves selected message attachments and inline images.

Uses bounded, atomic attachment extraction. By default each message gets its own folder; Flatten uses message-prefixed filenames in one folder. Original filenames remain in PassThru records. Existing files are never overwritten. Source files must remain unchanged.

## SYNTAX
### __AllParameterSets
```powershell
Save-MailAttachment [-Path] <string> -InputObject <Object> [-Flatten] [-PassThru] [-FileName <string[]>] [-ContentType <string[]>] [-InlineOnly] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Saves selected message attachments and inline images.

Uses bounded, atomic attachment extraction. By default each message gets its own folder; Flatten uses message-prefixed filenames in one folder. Original filenames remain in PassThru records. Existing files are never overwritten. Source files must remain unchanged.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-MailAttachment -Path 'C:\Path' -InputObject 'Value'
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

### -Flatten
Saves all messages into one directory with source-specific filename prefixes.

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

### -PassThru
Returns saved paths, attachment metadata and source provenance.

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

### -Path
Destination directory.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `Mailozaurr.MailSavedAttachment`

## RELATED LINKS

- None
