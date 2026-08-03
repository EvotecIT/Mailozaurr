---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-GraphMailboxPermission
## SYNOPSIS
Removes mailbox permissions via Microsoft Graph.

## SYNTAX
### Graph
```powershell
Remove-GraphMailboxPermission -UserPrincipalName <string> [-PermissionId <string[]>] [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Object
```powershell
Remove-GraphMailboxPermission -UserPrincipalName <string> [-MailboxPermission <GraphMailboxPermission[]>] [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Filter
```powershell
Remove-GraphMailboxPermission -UserPrincipalName <string> [-Role <GraphMailboxRole[]>] [-GrantedToUser <string[]>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Csv
```powershell
Remove-GraphMailboxPermission -UserPrincipalName <string> -CsvPath <string> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Remove-GraphMailboxPermission -UserPrincipalName <string> -MgGraphRequest [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes mailbox permissions via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-GraphMailboxPermission -UserPrincipalName 'Name' -CsvPath 'C:\Path'
```


### EXAMPLE 2
```powershell
Remove-GraphMailboxPermission -UserPrincipalName 'Name'
```


### EXAMPLE 3
```powershell
Remove-GraphMailboxPermission -UserPrincipalName 'Name' -MgGraphRequest
```


## PARAMETERS

### -Connection
Graph connection to use for the request.

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
Path to CSV file containing permission entries.

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

### -GrantedToUser
Filters permissions by assigned users.

```yaml
Type: String[]
Parameter Sets: Filter
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MailboxPermission
Mailbox permission objects to remove.

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
Switch to use Invoke-MgGraphRequest instead of built-in logic.

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

### -PermissionId
Identifier of permissions to remove.

```yaml
Type: String[]
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
Parameter Sets: Graph, Object, Filter, Csv, MgGraphRequest
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
Parameter Sets: Graph, Object, Filter, Csv, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Role
Filters permissions by mailbox role.

```yaml
Type: GraphMailboxRole[]
Parameter Sets: Filter
Aliases: None
Possible values: Owner, Read, Write, Custom

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
Parameter Sets: Graph, Object, Filter, Csv, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
Mailbox owner user principal name.

```yaml
Type: String
Parameter Sets: Graph, Object, Filter, Csv, MgGraphRequest
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
