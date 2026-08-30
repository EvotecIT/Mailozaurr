---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-GraphMessageAttachment
## SYNOPSIS
Saves attachments from Microsoft Graph message objects.

## SYNTAX
### __AllParameterSets
```powershell
Save-GraphMessageAttachment -Attachment <Attachment[]> -Path <string> [-ConflictPolicy <AttachmentFileConflictPolicy>] [-Force] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Saves attachments from Microsoft Graph message objects.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-GraphMessageAttachment -Attachment @('Value') -Path 'C:\Path'
```


## PARAMETERS

### -Attachment
Attachments to save from the message.

```yaml
Type: Attachment[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ConflictPolicy
Controls how existing destination files are handled.

```yaml
Type: AttachmentFileConflictPolicy
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Fail, Skip, Rename, Replace

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Replaces existing regular files. Equivalent to ConflictPolicy Replace.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: Overwrite
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Writes one save result for each attachment.

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
Destination path for saved attachments.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.Attachment[]`

## OUTPUTS

- `None`

## RELATED LINKS

- None
