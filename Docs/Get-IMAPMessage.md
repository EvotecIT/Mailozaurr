---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-IMAPMessage
## SYNOPSIS
Retrieves messages from an IMAP folder using optional filters.

The Get-IMAPMessage cmdlet fetches messages from the current IMAP folder associated with the provided ImapConnectionInfo object. You can filter by subject, sender, recipients, priority, date range and attachment presence. Messages can also be deleted after retrieval.

## SYNTAX
### Sequence (Default)
```powershell
Get-IMAPMessage [[-Client] <ImapConnectionInfo>] [[-FolderAccess] <FolderAccess>] [[-SequenceStart] <int>] [[-SequenceEnd] <int>] [-SearchQuery <SearchQuery[]>] [-Subject <string>] [-FromContains <string>] [-ToContains <string>] [-Priority <MessagePriority>] [-HasAttachment] [-All] [-Delete] [-Since <datetime>] [-Before <datetime>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Uid
```powershell
Get-IMAPMessage [[-Client] <ImapConnectionInfo>] [[-FolderAccess] <FolderAccess>] [[-UidStart] <uint>] [[-UidEnd] <uint>] [-SearchQuery <SearchQuery[]>] [-Subject <string>] [-FromContains <string>] [-ToContains <string>] [-Priority <MessagePriority>] [-HasAttachment] [-All] [-Delete] [-Since <datetime>] [-Before <datetime>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Retrieves messages from an IMAP folder using optional filters.

The Get-IMAPMessage cmdlet fetches messages from the current IMAP folder associated with the provided ImapConnectionInfo object. You can filter by subject, sender, recipients, priority, date range and attachment presence. Messages can also be deleted after retrieval.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-IMAPMessage -All
```


## PARAMETERS

### -All
If set, retrieves all messages ignoring other filters.

```yaml
Type: SwitchParameter
Parameter Sets: Sequence, Uid
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
Parameter Sets: Sequence, Uid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Client
The ImapConnectionInfo object representing the active IMAP connection. This is the object returned by Connect-IMAP.

```yaml
Type: ImapConnectionInfo
Parameter Sets: Sequence, Uid
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Delete
If set, deletes the retrieved messages.

```yaml
Type: SwitchParameter
Parameter Sets: Sequence, Uid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FolderAccess
Specifies the folder access mode (ReadOnly or ReadWrite). Default is ReadOnly.

```yaml
Type: FolderAccess
Parameter Sets: Sequence, Uid
Aliases: None
Possible values: None, ReadOnly, ReadWrite

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FromContains
Only return messages sent from addresses matching this value.

```yaml
Type: String
Parameter Sets: Sequence, Uid
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
Parameter Sets: Sequence, Uid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Priority
Only return messages with the specified priority.

```yaml
Type: Nullable`1
Parameter Sets: Sequence, Uid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SearchQuery
Search queries used to match messages to retrieve.

```yaml
Type: SearchQuery[]
Parameter Sets: Sequence, Uid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SequenceEnd
Specifies the ending sequence number of messages to retrieve. If not provided, only SequenceStart is fetched.

```yaml
Type: Nullable`1
Parameter Sets: Sequence
Aliases: None
Possible values:

Required: False
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SequenceStart
Specifies the starting sequence number of messages to retrieve.

```yaml
Type: Nullable`1
Parameter Sets: Sequence
Aliases: None
Possible values:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Since
Return messages delivered on or after this date.

```yaml
Type: Nullable`1
Parameter Sets: Sequence, Uid
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
Parameter Sets: Sequence, Uid
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
Parameter Sets: Sequence, Uid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UidEnd
Specifies the ending UID of messages to retrieve. If not provided, only UidStart is fetched.

```yaml
Type: Nullable`1
Parameter Sets: Uid
Aliases: None
Possible values:

Required: False
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UidStart
Specifies the starting UID of messages to retrieve.

```yaml
Type: Nullable`1
Parameter Sets: Uid
Aliases: None
Possible values:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.ImapConnectionInfo`: Represents the result of a successful IMAP connection, including the client and connection details.

## OUTPUTS

- `Mailozaurr.ImapMessageInfo`

## RELATED LINKS

- CmdletConnectIMAP
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
