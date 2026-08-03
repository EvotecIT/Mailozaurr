---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Clear-GraphJunk
## SYNOPSIS
Clears the Junk Email folder via Microsoft Graph.

## SYNTAX
### Graph
```powershell
Clear-GraphJunk -UserPrincipalName <string> [-Connection <GraphConnectionInfo>] [-Property <string[]>] [-Preview] [-SkipId <string[]>] [-SkipFrom <string[]>] [-SkipTo <string[]>] [-SkipSubjectContains <string[]>] [-SkipHasAttachment] [-SkipAttachmentExtension <string[]>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Clear-GraphJunk -UserPrincipalName <string> -MgGraphRequest [-Property <string[]>] [-Preview] [-SkipId <string[]>] [-SkipFrom <string[]>] [-SkipTo <string[]>] [-SkipSubjectContains <string[]>] [-SkipHasAttachment] [-SkipAttachmentExtension <string[]>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Clears the Junk Email folder via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
Clear-GraphJunk -UserPrincipalName 'Name'
```


### EXAMPLE 2
```powershell
Clear-GraphJunk -UserPrincipalName 'Name' -MgGraphRequest
```


## PARAMETERS

### -Connection
Connection information for Microsoft Graph.

```yaml
Type: GraphConnectionInfo
Parameter Sets: Graph
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaxConcurrentRequests
Maximum number of concurrent Graph requests.

```yaml
Type: Int32
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MgGraphRequest
Returns the Microsoft Graph request payload instead of sending it.

```yaml
Type: SwitchParameter
Parameter Sets: MgGraphRequest
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Preview
When present, only outputs the messages that would be removed.

```yaml
Type: SwitchParameter
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Property
Additional message properties to retrieve.

```yaml
Type: String[]
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryCount
Number of retry attempts on failure.

```yaml
Type: Int32
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryDelayMilliseconds
Delay between retries in milliseconds.

```yaml
Type: Int32
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipAttachmentExtension
Attachment file extensions to exclude.

```yaml
Type: String[]
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipFrom
Sender addresses to exclude from deletion.

```yaml
Type: String[]
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipHasAttachment
Skip messages that have attachments.

```yaml
Type: SwitchParameter
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipId
Message identifiers that should not be removed.

```yaml
Type: String[]
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipSubjectContains
Skips messages when the subject contains any of these strings.

```yaml
Type: String[]
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipTo
Recipient addresses to exclude from deletion.

```yaml
Type: String[]
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutSeconds
Timeout for Graph requests in seconds.

```yaml
Type: Int32
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
User principal name of the mailbox to clean.

```yaml
Type: String
Parameter Sets: Graph, MgGraphRequest
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

- `None`

## RELATED LINKS

- None
