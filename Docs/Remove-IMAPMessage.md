---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-IMAPMessage
## SYNOPSIS
Removes messages from an IMAP folder by UID.

## SYNTAX
### __AllParameterSets
```powershell
Remove-IMAPMessage [[-Client] <ImapConnectionInfo>] [-Uid] <uint[]> [[-Folder] <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes messages from an IMAP folder by UID.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-IMAPMessage -Client 'Value'
```


## PARAMETERS

### -Client
Active IMAP connection info.

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
Optional folder containing the messages.

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
UIDs of messages to delete.

```yaml
Type: UInt32[]
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
