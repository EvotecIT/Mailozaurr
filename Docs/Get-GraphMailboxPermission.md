---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-GraphMailboxPermission
## SYNOPSIS
Retrieves mailbox permissions for a user via Microsoft Graph.

## SYNTAX
### Graph
```powershell
Get-GraphMailboxPermission -UserPrincipalName <string> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Get-GraphMailboxPermission -UserPrincipalName <string> -MgGraphRequest [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves mailbox permissions for a user via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-GraphMailboxPermission -UserPrincipalName 'Name'
```


### EXAMPLE 2
```powershell
Get-GraphMailboxPermission -UserPrincipalName 'Name' -MgGraphRequest
```


## PARAMETERS

### -Connection
Connection to Microsoft Graph.

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
Number of retries on transient failures.

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
User principal name owning the mailbox.

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

- `Mailozaurr.GraphMailboxPermission`

## RELATED LINKS

- None
