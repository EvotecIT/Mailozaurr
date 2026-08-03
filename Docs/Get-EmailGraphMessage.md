---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-EmailGraphMessage
## SYNOPSIS
Retrieves mail messages for a user via Microsoft Graph.

The Get-EmailGraphMessage cmdlet fetches messages for the specified user principal name using Microsoft Graph. It supports optional filters like subject, sender, recipient, priority and date range. Results can be limited and optionally deleted.

## SYNTAX
### Graph
```powershell
Get-EmailGraphMessage -UserPrincipalName <string> [-Connection <GraphConnectionInfo>] [-Property <string[]>] [-Filter <string>] [-Limit <Int32>] [-Subject <string>] [-FromContains <string>] [-ToContains <string>] [-Priority <MessagePriority>] [-Since <DateTime>] [-Before <DateTime>] [-HasAttachment] [-All] [-Delete] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Get-EmailGraphMessage -UserPrincipalName <string> -MgGraphRequest [-Property <string[]>] [-Filter <string>] [-Limit <Int32>] [-Subject <string>] [-FromContains <string>] [-ToContains <string>] [-Priority <MessagePriority>] [-Since <DateTime>] [-Before <DateTime>] [-HasAttachment] [-All] [-Delete] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Retrieves mail messages for a user via Microsoft Graph.

The Get-EmailGraphMessage cmdlet fetches messages for the specified user principal name using Microsoft Graph. It supports optional filters like subject, sender, recipient, priority and date range. Results can be limited and optionally deleted.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EmailGraphMessage -UserPrincipalName 'Name'
```


### EXAMPLE 2
```powershell
Get-EmailGraphMessage -UserPrincipalName 'Name' -MgGraphRequest
```


## PARAMETERS

### -All
When present, retrieves all messages ignoring limit.

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

### -Before
Retrieves messages received before this date.

```yaml
Type: DateTime
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Graph connection information.

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

### -Delete
Deletes messages after retrieval when set.

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

### -Filter
Raw OData filter passed directly to Microsoft Graph.

```yaml
Type: String
Parameter Sets: Graph, MgGraphRequest
Aliases: ODataFilter
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FromContains
Filters messages where the sender contains this value.

```yaml
Type: String
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HasAttachment
Filters messages that have attachments.

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

### -Limit
Limits the number of returned messages.

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
When specified, uses Invoke-MgGraphRequest for the operation.

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

### -Priority
Filters messages by importance.

```yaml
Type: MessagePriority
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values: High, Low, Normal

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Property
Message properties to select.

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

### -Since
Retrieves messages received since this date.

```yaml
Type: DateTime
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Subject
Filters messages by subject text.

```yaml
Type: String
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
Request timeout in seconds.

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

### -ToContains
Filters messages where the recipient contains this value.

```yaml
Type: String
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
User principal name whose mailbox is queried.

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

- `Mailozaurr.GraphMessageInfo`

## RELATED LINKS

- None
