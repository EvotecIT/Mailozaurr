---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-POP3MessageAttachment
## SYNOPSIS
Saves attachments from a POP3 message to disk.

The Save-POP3MessageAttachment cmdlet saves all attachments from a POP3 message identified by its index to the specified directory.

## SYNTAX
### __AllParameterSets
```powershell
Save-POP3MessageAttachment [[-Client] <PopConnectionInfo>] [-Index] <int> [-Path] <string> [-ConflictPolicy <AttachmentFileConflictPolicy>] [-Force] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Saves attachments from a POP3 message to disk.

The Save-POP3MessageAttachment cmdlet saves all attachments from a POP3 message identified by its index to the specified directory.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-POP3MessageAttachment -Path 'C:\Path'
```


## PARAMETERS

### -Client
The PopConnectionInfo object representing the active POP3 connection.

```yaml
Type: PopConnectionInfo
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
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

### -Index
Specifies the index of the message to process.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
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
Specifies the directory path where attachments will be saved.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.PopConnectionInfo`: Represents the result of a successful POP3 connection, including the client and connection details.

## OUTPUTS

- `None`

## RELATED LINKS

- None
