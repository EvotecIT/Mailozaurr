---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-IMAPMessageAttachment
## SYNOPSIS
Saves attachments from an IMAP message to disk.

The Save-IMAPMessageAttachment cmdlet saves all attachments from an IMAP message identified by its UID to the specified directory.

## SYNTAX
### __AllParameterSets
```powershell
Save-IMAPMessageAttachment [[-Client] <ImapConnectionInfo>] [-Uid] <uint> [[-Folder] <string>] [-Path] <string> [-ConflictPolicy <AttachmentFileConflictPolicy>] [-Force] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Saves attachments from an IMAP message to disk.

The Save-IMAPMessageAttachment cmdlet saves all attachments from an IMAP message identified by its UID to the specified directory.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-IMAPMessageAttachment -Path 'C:\Path'
```


## PARAMETERS

### -Client
The ImapConnectionInfo object representing the active IMAP connection.

```yaml
Type: ImapConnectionInfo
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

### -Folder
Optional folder from which to retrieve the message. Defaults to Inbox.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 2
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
Specifies the directory path where attachments will be saved.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Uid
Specifies the UID of the message to process.

```yaml
Type: UInt32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.ImapConnectionInfo`: Represents the result of a successful IMAP connection, including the client and connection details.

## OUTPUTS

- `None`

## RELATED LINKS

- None
