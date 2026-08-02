---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Add-GraphMailboxPermission
## SYNOPSIS
Adds mailbox permissions via Microsoft Graph.

## SYNTAX
### Graph
```powershell
Add-GraphMailboxPermission -UserPrincipalName <string> [-Permission <hashtable[]>] [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Object
```powershell
Add-GraphMailboxPermission -UserPrincipalName <string> [-MailboxPermission <GraphMailboxPermission[]>] [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Csv
```powershell
Add-GraphMailboxPermission -UserPrincipalName <string> -CsvPath <string> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Add-GraphMailboxPermission -UserPrincipalName <string> -MgGraphRequest [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Adds mailbox permissions via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
Add-GraphMailboxPermission -UserPrincipalName 'Name' -CsvPath 'C:\Path'
```


### EXAMPLE 2
```powershell
Add-GraphMailboxPermission -UserPrincipalName 'Name'
```


### EXAMPLE 3
```powershell
Add-GraphMailboxPermission -UserPrincipalName 'Name' -MgGraphRequest
```


## PARAMETERS

### -Connection
Graph connection information.

```yaml
Type: GraphConnectionInfo
Parameter Sets: Graph, Object, Csv
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -CsvPath
Path to a CSV file containing permission definitions.

```yaml
Type: String
Parameter Sets: Csv
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MailboxPermission
Mailbox permission objects provided via the pipeline.

```yaml
Type: GraphMailboxPermission[]
Parameter Sets: Object
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MgGraphRequest
Indicates that the raw Microsoft Graph request should be returned.

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

### -Permission
Permission definitions when using the Graph parameter set.

```yaml
Type: Hashtable[]
Parameter Sets: Graph
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
Parameter Sets: Graph, Object, Csv, MgGraphRequest
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
Parameter Sets: Graph, Object, Csv, MgGraphRequest
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
Parameter Sets: Graph, Object, Csv, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
User principal name of the mailbox owner.

```yaml
Type: String
Parameter Sets: Graph, Object, Csv, MgGraphRequest
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

- `Mailozaurr.GraphMailboxPermission[]`
- `Mailozaurr.PowerShell.GraphConnectionInfo`: Represents an authenticated Microsoft Graph connection.

## OUTPUTS

- `None`

## RELATED LINKS

- None
