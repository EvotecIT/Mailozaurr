---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Wait-GraphMessage
## SYNOPSIS
Waits for new Graph messages by polling Microsoft Graph.

The Wait-GraphMessage cmdlet listens for new messages for the specified user principal name. Messages are written to the pipeline as they arrive.

## SYNTAX
### __AllParameterSets
```powershell
Wait-GraphMessage -UserPrincipalName <string> [-Connection <GraphConnectionInfo>] [-Action <scriptblock>] [-Until <scriptblock>] [-StopOnMatch] [-TimeoutSeconds <int>] [<CommonParameters>]
```

## DESCRIPTION
Waits for new Graph messages by polling Microsoft Graph.

The Wait-GraphMessage cmdlet listens for new messages for the specified user principal name. Messages are written to the pipeline as they arrive.

## EXAMPLES

### EXAMPLE 1
```powershell
Wait-GraphMessage -UserPrincipalName 'Name'
```


## PARAMETERS

### -Action
Optional action executed for each received message.

```yaml
Type: ScriptBlock
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Graph connection information used when polling for messages.

```yaml
Type: GraphConnectionInfo
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -StopOnMatch
Stops waiting when a message matches the Until condition.

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

### -TimeoutSeconds
Optional timeout after which listening will stop automatically.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Until
Script block that determines when to stop waiting for messages.

```yaml
Type: ScriptBlock
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
User principal name whose mailbox should be monitored.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.GraphConnectionInfo`: Represents an authenticated Microsoft Graph connection.

## OUTPUTS

- `System.Object`

## RELATED LINKS

- None
