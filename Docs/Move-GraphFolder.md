---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Move-GraphFolder
## SYNOPSIS
Moves a Microsoft Graph mail folder.

## SYNTAX
### Parent
```powershell
Move-GraphFolder -UserPrincipalName <string> -FolderId <string> -DestinationFolderId <string> [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Root
```powershell
Move-GraphFolder -UserPrincipalName <string> -FolderId <string> [-Root] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Graph
```powershell
Move-GraphFolder -UserPrincipalName <string> -FolderId <string> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Move-GraphFolder -UserPrincipalName <string> -FolderId <string> -MgGraphRequest [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Moves a Microsoft Graph mail folder.

## EXAMPLES

### EXAMPLE 1
```powershell
Move-GraphFolder -UserPrincipalName 'Name' -FolderId 'Value'
```


### EXAMPLE 2
```powershell
Move-GraphFolder -UserPrincipalName 'Name' -FolderId 'Value' -MgGraphRequest
```


### EXAMPLE 3
```powershell
Move-GraphFolder -UserPrincipalName 'Name' -FolderId 'Value' -DestinationFolderId 'Value'
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

### -DestinationFolderId
Identifier of the destination folder.

```yaml
Type: String
Parameter Sets: Parent
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FolderId
Identifier of the folder to move.

```yaml
Type: String
Parameter Sets: Parent, Root, Graph, MgGraphRequest
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConcurrentRequests
Maximum number of concurrent requests during the move operation.

```yaml
Type: Int32
Parameter Sets: Parent, Root, Graph, MgGraphRequest
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

### -RetryCount
Number of retries on transient errors.

```yaml
Type: Int32
Parameter Sets: Parent, Root, Graph, MgGraphRequest
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
Parameter Sets: Parent, Root, Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Root
Move folder to the root.

```yaml
Type: SwitchParameter
Parameter Sets: Root
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
Parameter Sets: Parent, Root, Graph, MgGraphRequest
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
Parameter Sets: Parent, Root, Graph, MgGraphRequest
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
