---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-EmailGraphMessageAttachment
## SYNOPSIS
Retrieves attachments for a specific mail message via Microsoft Graph API.

The Get-EmailGraphMessageAttachment cmdlet retrieves attachments for the specified mail message ID and user principal name using Microsoft Graph API. Provide a GraphConnectionInfo object created with Connect-EmailGraph or authenticate via Connect-MgGraph.

## SYNTAX
### Graph
```powershell
Get-EmailGraphMessageAttachment -UserPrincipalName <string> -MessageId <string> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-Property <string[]>] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Get-EmailGraphMessageAttachment -UserPrincipalName <string> -MessageId <string> -MgGraphRequest [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-Property <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves attachments for a specific mail message via Microsoft Graph API.

The Get-EmailGraphMessageAttachment cmdlet retrieves attachments for the specified mail message ID and user principal name using Microsoft Graph API. Provide a GraphConnectionInfo object created with Connect-EmailGraph or authenticate via Connect-MgGraph.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EmailGraphMessageAttachment -UserPrincipalName 'Name' -MessageId 'Value'
```


### EXAMPLE 2
```powershell
Get-EmailGraphMessageAttachment -UserPrincipalName 'Name' -MessageId 'Value' -MgGraphRequest
```


## PARAMETERS

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

### -MessageId
Specifies the message ID for which attachments will be retrieved.

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

### -MgGraphRequest
Executes the request via Invoke-MgGraphRequest when set.

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

### -Property
Specifies the properties to retrieve for each attachment.

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

### -UserPrincipalName
Specifies the user principal name (email address) whose mail message attachments will be retrieved.

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

- `Mailozaurr.Attachment`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
