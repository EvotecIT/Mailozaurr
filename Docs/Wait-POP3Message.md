---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Wait-POP3Message
## SYNOPSIS
Waits for new POP3 messages by polling the server.

The Wait-POP3Message cmdlet listens for new messages arriving in the connected POP3 mailbox. Messages are written to the pipeline as they are received.

## SYNTAX
### __AllParameterSets
```powershell
Wait-POP3Message [-Client <PopConnectionInfo>] [-Action <scriptblock>] [-Until <scriptblock>] [-StopOnMatch] [-TimeoutSeconds <int>] [<CommonParameters>]
```

## DESCRIPTION
Waits for new POP3 messages by polling the server.

The Wait-POP3Message cmdlet listens for new messages arriving in the connected POP3 mailbox. Messages are written to the pipeline as they are received.

## EXAMPLES

### EXAMPLE 1
```powershell
Wait-POP3Message -Action { }
```


## PARAMETERS

### -Action
Optional action invoked for each message that arrives.

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

### -Client
Connection information for the POP3 session used for polling.

```yaml
Type: PopConnectionInfo
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
Stops polling when the Until condition is satisfied.

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
Optional timeout after which the cmdlet stops polling.

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
Script block determining when to stop waiting for messages.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.PopConnectionInfo`: Represents the result of a successful POP3 connection, including the client and connection details.

## OUTPUTS

- `Mailozaurr.Pop3EmailMessage`

## RELATED LINKS

- None
