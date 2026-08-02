---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-GraphInboxRule
## SYNOPSIS
Retrieves inbox rules for a mailbox via Microsoft Graph.

## SYNTAX
### Graph
```powershell
Get-GraphInboxRule -UserPrincipalName <string> [-Connection <GraphConnectionInfo>] [-Filter <string>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Get-GraphInboxRule -UserPrincipalName <string> -MgGraphRequest [-Filter <string>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves inbox rules for a mailbox via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-GraphInboxRule -UserPrincipalName 'Name'
```


### EXAMPLE 2
```powershell
Get-GraphInboxRule -UserPrincipalName 'Name' -MgGraphRequest
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

### -Filter
Optional OData filter string.

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

### -MgGraphRequest
Indicates the use of Invoke-MgGraphRequest for this operation.

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
User principal name whose inbox rules are retrieved.

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

- `Mailozaurr.GraphInboxRule`

## RELATED LINKS

- None
