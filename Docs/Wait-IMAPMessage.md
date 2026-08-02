---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Wait-IMAPMessage
## SYNOPSIS
Waits for new IMAP messages using the IMAP IDLE command.

The Wait-IMAPMessage cmdlet listens for new messages arriving in the specified folder and writes them to the pipeline as they are received.

## SYNTAX
### __AllParameterSets
```powershell
Wait-IMAPMessage [-Client <ImapConnectionInfo>] [-Folder <string>] [-SearchQuery <SearchQuery[]>] [-Action <scriptblock>] [-Until <scriptblock>] [-StopOnMatch] [-TimeoutSeconds <int>] [<CommonParameters>]
```

## DESCRIPTION
Waits for new IMAP messages using the IMAP IDLE command.

The Wait-IMAPMessage cmdlet listens for new messages arriving in the specified folder and writes them to the pipeline as they are received.

## EXAMPLES

### EXAMPLE 1
```powershell
Wait-IMAPMessage -Action { }
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

### -Client
The ImapConnectionInfo representing the active IMAP session. Defaults to the last session created by Connect-IMAP.

```yaml
Type: ImapConnectionInfo
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Folder
Optional folder name to monitor. Defaults to Inbox.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SearchQuery
Additional MailKit search queries applied when listening for messages.

```yaml
Type: SearchQuery[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StopOnMatch
Stops listening when the Until condition is satisfied.

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
Optional timeout after which the cmdlet stops waiting.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.ImapConnectionInfo`: Represents the result of a successful IMAP connection, including the client and connection details.

## OUTPUTS

- `Mailozaurr.ImapEmailMessage`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
