---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-POP3Message
## SYNOPSIS
Saves a POP3 message to disk in either EML or MSG format.

The Save-POP3Message cmdlet saves a message from a POP3 mailbox (using a PopConnectionInfo object from Connect-POP3) to disk at the specified path. Use this to archive, export, or process messages retrieved from a POP3 server. The message can be saved as an EML file or converted to MSG format.

## SYNTAX
### __AllParameterSets
```powershell
Save-POP3Message [[-Client] <PopConnectionInfo>] [-Index] <int> [-Path] <string> [<CommonParameters>]
```

## DESCRIPTION
Saves a POP3 message to disk in either EML or MSG format.

The Save-POP3Message cmdlet saves a message from a POP3 mailbox (using a PopConnectionInfo object from Connect-POP3) to disk at the specified path. Use this to archive, export, or process messages retrieved from a POP3 server. The message can be saved as an EML file or converted to MSG format.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-POP3Message -Path 'C:\Path'
```


## PARAMETERS

### -Client
The PopConnectionInfo object representing the active POP3 connection. This is the object returned by Connect-POP3.

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

### -Index
Specifies the index of the message to save.

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

### -Path
Specifies the path where the message will be saved.

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

- CmdletConnectPOP3
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
