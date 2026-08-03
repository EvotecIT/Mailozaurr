---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-IMAPMessage
## SYNOPSIS
Saves an IMAP message to disk at the specified path.

The Save-IMAPMessage cmdlet saves a message from an IMAP mailbox (using a ImapConnectionInfo object from Connect-IMAP) to disk at the given path. Provide the unique identifier of the message to export or archive it.

## SYNTAX
### __AllParameterSets
```powershell
Save-IMAPMessage [[-Client] <ImapConnectionInfo>] [-Uid] <uint> [[-Folder] <string>] [-Path] <string> [<CommonParameters>]
```

## DESCRIPTION
Saves an IMAP message to disk at the specified path.

The Save-IMAPMessage cmdlet saves a message from an IMAP mailbox (using a ImapConnectionInfo object from Connect-IMAP) to disk at the given path. Provide the unique identifier of the message to export or archive it.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-IMAPMessage -Path 'C:\Path'
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

### -Folder
Optional folder name from which to fetch the message. Defaults to Inbox.

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

### -Path
Specifies the path where the message will be saved.

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
Specifies the UID of the message to save.

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
