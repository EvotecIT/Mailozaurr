---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Disconnect-EmailGraph
## SYNOPSIS
Clears Microsoft Graph credentials created by Connect-EmailGraph.

The Disconnect-EmailGraph cmdlet removes sensitive
information from a GraphConnectionInfo object returned by
Connect-EmailGraph. Use it when you no longer need the connection to
ensure credentials are disposed and not kept in memory.

## SYNTAX
### __AllParameterSets
```powershell
Disconnect-EmailGraph [-Connection] <GraphConnectionInfo> [<CommonParameters>]
```

## DESCRIPTION
Clears Microsoft Graph credentials created by Connect-EmailGraph.

The Disconnect-EmailGraph cmdlet removes sensitive
information from a GraphConnectionInfo object returned by
Connect-EmailGraph. Use it when you no longer need the connection to
ensure credentials are disposed and not kept in memory.

## EXAMPLES

### EXAMPLE 1
```powershell
Disconnect-EmailGraph -Connection 'Value'
```


## PARAMETERS

### -Connection
The GraphConnectionInfo object to clear.

```yaml
Type: GraphConnectionInfo
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

- `Mailozaurr.PowerShell.GraphConnectionInfo`: Represents an authenticated Microsoft Graph connection.

## OUTPUTS

- `None`

## RELATED LINKS

- CmdletConnectEmailGraph
