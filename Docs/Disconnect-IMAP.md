---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Disconnect-IMAP
## SYNOPSIS
Disconnects an active IMAP connection previously established with Connect-IMAP.

The Disconnect-IMAP cmdlet disconnects an active MailKit IMAP client session. Pass the ImapConnectionInfo object returned by Connect-IMAP to this cmdlet to safely close the connection and release resources.

## SYNTAX
### __AllParameterSets
```powershell
Disconnect-IMAP [-Client] <ImapConnectionInfo> [<CommonParameters>]
```

## DESCRIPTION
Disconnects an active IMAP connection previously established with Connect-IMAP.

The Disconnect-IMAP cmdlet disconnects an active MailKit IMAP client session. Pass the ImapConnectionInfo object returned by Connect-IMAP to this cmdlet to safely close the connection and release resources.

## EXAMPLES

### EXAMPLE 1
```powershell
Disconnect-IMAP -Client 'Value'
```


## PARAMETERS

### -Client
The ImapConnectionInfo object containing the MailKit IMAP client instance to disconnect. This is the object returned by Connect-IMAP.

```yaml
Type: ImapConnectionInfo
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
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
