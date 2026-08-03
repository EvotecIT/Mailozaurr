---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Set-IMAPFolder
## SYNOPSIS
Sets the working IMAP folder for subsequent operations.

## SYNTAX
### __AllParameterSets
```powershell
Set-IMAPFolder [[-Client] <ImapConnectionInfo>] [-Path] <string> [-FolderAccess <FolderAccess>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Sets the working IMAP folder for subsequent operations.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-IMAPFolder -Path 'C:\Path'
```


## PARAMETERS

### -Client
Active IMAP connection.

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

### -FolderAccess
Folder access mode.

```yaml
Type: FolderAccess
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: None, ReadOnly, ReadWrite

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Folder path to open.

```yaml
Type: String
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
