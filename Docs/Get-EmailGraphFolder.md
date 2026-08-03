---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-EmailGraphFolder
## SYNOPSIS
Retrieves mail folders for a user via Microsoft Graph API.

The Get-EmailGraphFolder cmdlet retrieves mail folders for the specified user principal name using Microsoft Graph API. Provide a GraphConnectionInfo object created with Connect-EmailGraph or authenticate via Connect-MgGraph.

## SYNTAX
### Graph
```powershell
Get-EmailGraphFolder -UserPrincipalName <string> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Get-EmailGraphFolder -UserPrincipalName <string> -MgGraphRequest [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves mail folders for a user via Microsoft Graph API.

The Get-EmailGraphFolder cmdlet retrieves mail folders for the specified user principal name using Microsoft Graph API. Provide a GraphConnectionInfo object created with Connect-EmailGraph or authenticate via Connect-MgGraph.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EmailGraphFolder -UserPrincipalName 'Name'
```


### EXAMPLE 2
```powershell
Get-EmailGraphFolder -UserPrincipalName 'Name' -MgGraphRequest
```


## PARAMETERS

### -Connection
Graph connection context to use when performing the request.

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
Maximum parallel Microsoft Graph requests.

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
Switch indicating that Invoke-MgGraphRequest should be used.

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
Number of retry attempts when requests fail.

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
Delay between retry attempts in milliseconds.

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
Specifies the user principal name (email address) whose mail folders will be retrieved.

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

- `System.Object`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
