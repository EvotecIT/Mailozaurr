---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Move-IMAPMessage
## SYNOPSIS
Moves an IMAP message to another folder.

The Move-IMAPMessage cmdlet moves a message identified by its UID from the current folder to the specified destination folder.

## SYNTAX
### __AllParameterSets
```powershell
Move-IMAPMessage [[-Client] <ImapConnectionInfo>] [-Uid] <uint> [[-SourceFolder] <string>] [-DestinationFolder] <string> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Moves an IMAP message to another folder.

The Move-IMAPMessage cmdlet moves a message identified by its UID from the current folder to the specified destination folder.

## EXAMPLES

### EXAMPLE 1
```powershell
Move-IMAPMessage -Client 'Value'
```


## PARAMETERS

### -Client
The ImapConnectionInfo object representing the active IMAP connection. This is the object returned by Connect-IMAP.

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

### -DestinationFolder
Destination folder where the message should be moved.

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

### -SourceFolder
Optional source folder of the message. Defaults to Inbox.

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

### -Uid
UID of the message to move.

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

- CmdletConnectIMAP
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
