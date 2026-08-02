---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-POP3Message
## SYNOPSIS
Retrieves messages from a POP3 mailbox with optional filters.

The Get-POP3Message cmdlet fetches messages from a POP3 mailbox using the provided PopConnectionInfo object. It supports filtering by subject, sender, recipients, priority, date range and attachment presence. Messages can be removed after retrieval using -Delete.

## SYNTAX
### __AllParameterSets
```powershell
Get-POP3Message [[-Client] <PopConnectionInfo>] [[-Index] <int>] [[-Count] <int>] [-All] [-Subject <string>] [-FromContains <string>] [-ToContains <string>] [-Priority <MessagePriority>] [-Since <datetime>] [-Before <datetime>] [-HasAttachment] [-Delete] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Retrieves messages from a POP3 mailbox with optional filters.

The Get-POP3Message cmdlet fetches messages from a POP3 mailbox using the provided PopConnectionInfo object. It supports filtering by subject, sender, recipients, priority, date range and attachment presence. Messages can be removed after retrieval using -Delete.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-POP3Message -All
```


## PARAMETERS

### -All
If set, retrieves all messages from the POP3 mailbox.

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

### -Before
Return messages delivered on or before this date.

```yaml
Type: Nullable`1
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

### -Count
Specifies the number of messages to retrieve starting from Index. Default is 1.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Delete
If set, deletes the retrieved messages.

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

### -FromContains
Only return messages sent from addresses matching this value.

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

### -HasAttachment
Only return messages that contain attachments.

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

### -Index
Specifies the index of the first message to retrieve. Default is 0 (the first message).

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Priority
Only return messages with the specified priority.

```yaml
Type: Nullable`1
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Since
Return messages delivered on or after this date.

```yaml
Type: Nullable`1
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Subject
Only return messages containing this text in the subject.

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

### -ToContains
Only return messages sent to addresses matching this value.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.PopConnectionInfo`: Represents the result of a successful POP3 connection, including the client and connection details.

## OUTPUTS

- `Mailozaurr.Pop3MessageInfo`

## RELATED LINKS

- CmdletConnectPOP3
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
