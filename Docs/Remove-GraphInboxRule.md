---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-GraphInboxRule
## SYNOPSIS
Removes an inbox rule via Microsoft Graph.

## SYNTAX
### Graph
```powershell
Remove-GraphInboxRule -UserPrincipalName <string> -RuleId <string> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Remove-GraphInboxRule -UserPrincipalName <string> -RuleId <string> -MgGraphRequest [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes an inbox rule via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-GraphInboxRule -UserPrincipalName 'Name' -RuleId 'Value'
```


### EXAMPLE 2
```powershell
Remove-GraphInboxRule -UserPrincipalName 'Name' -RuleId 'Value' -MgGraphRequest
```


## PARAMETERS

### -Connection
Graph connection context.

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

### -RuleId
Identifier of the rule to remove.

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
User principal name owning the inbox rule.

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
