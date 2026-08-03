---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Disconnect-POP3
## SYNOPSIS
Disconnects an active POP3 connection previously established with Connect-POP3.

The Disconnect-POP3 cmdlet disconnects an active MailKit POP3 client session. Pass the PopConnectionInfo object returned by Connect-POP3 to this cmdlet to safely close the connection and release resources.

## SYNTAX
### __AllParameterSets
```powershell
Disconnect-POP3 [-Client] <PopConnectionInfo> [<CommonParameters>]
```

## DESCRIPTION
Disconnects an active POP3 connection previously established with Connect-POP3.

The Disconnect-POP3 cmdlet disconnects an active MailKit POP3 client session. Pass the PopConnectionInfo object returned by Connect-POP3 to this cmdlet to safely close the connection and release resources.

## EXAMPLES

### EXAMPLE 1
```powershell
Disconnect-POP3 -Client 'Value'
```


## PARAMETERS

### -Client
The PopConnectionInfo object containing the MailKit POP3 client instance to disconnect. This is the object returned by Connect-POP3.

```yaml
Type: PopConnectionInfo
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

- `Mailozaurr.PowerShell.PopConnectionInfo`: Represents the result of a successful POP3 connection, including the client and connection details.

## OUTPUTS

- `None`

## RELATED LINKS

- CmdletConnectPOP3
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
