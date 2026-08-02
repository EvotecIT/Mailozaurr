---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Move-IMAPFolder
## SYNOPSIS
Moves an IMAP folder to a new location.

## SYNTAX
### Parent
```powershell
Move-IMAPFolder [[-Client] <ImapConnectionInfo>] [-Folder] <string> [-DestinationFolder] <string> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Root
```powershell
Move-IMAPFolder [[-Client] <ImapConnectionInfo>] [-Folder] <string> [-Root] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Moves an IMAP folder to a new location.

## EXAMPLES

### EXAMPLE 1
```powershell
Move-IMAPFolder -Client 'Value'
```


## PARAMETERS

### -Client
Active IMAP connection info.

```yaml
Type: ImapConnectionInfo
Parameter Sets: Parent, Root
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -DestinationFolder
Destination folder name.

```yaml
Type: String
Parameter Sets: Parent
Aliases: None
Possible values:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Folder
Name of the folder to move.

```yaml
Type: String
Parameter Sets: Parent, Root
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Root
Move folder to the root.

```yaml
Type: SwitchParameter
Parameter Sets: Root
Aliases: None
Possible values:

Required: False
Position: named
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
