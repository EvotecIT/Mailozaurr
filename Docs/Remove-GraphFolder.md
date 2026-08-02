---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-GraphFolder
## SYNOPSIS
Removes a Microsoft Graph mail folder.

## SYNTAX
### Graph
```powershell
Remove-GraphFolder -UserPrincipalName <string> -FolderId <string> [-Recursive] [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Remove-GraphFolder -UserPrincipalName <string> -FolderId <string> -MgGraphRequest [-Recursive] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes a Microsoft Graph mail folder.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-GraphFolder -UserPrincipalName 'Name' -FolderId 'Value'
```


### EXAMPLE 2
```powershell
Remove-GraphFolder -UserPrincipalName 'Name' -FolderId 'Value' -MgGraphRequest
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
Accept pipeline input: False
Accept wildcard characters: False
```

### -FolderId
Identifier of the folder to remove.

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

### -MaxConcurrentRequests
Maximum number of concurrent Microsoft Graph requests allowed.

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
Use Invoke-MgGraphRequest instead of built-in logic.

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

### -Recursive
Remove subfolders as well. Microsoft Graph always deletes recursively.

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

### -RetryCount
Number of retries on transient errors.

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
User principal name owning the folder.

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

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- None
