---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Search-GraphMailbox
## SYNOPSIS
Searches one or more mailboxes using Microsoft Graph.

The Search-GraphMailbox cmdlet queries Microsoft Graph using application permissions. Provide multiple user principal names to search across several mailboxes. Results are returned as GraphMessageInfo objects.

## SYNTAX
### Graph
```powershell
Search-GraphMailbox -UserPrincipalName <string[]> -Query <string> [-Connection <GraphConnectionInfo>] [-From <int>] [-Size <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

## DESCRIPTION
Searches one or more mailboxes using Microsoft Graph.

The Search-GraphMailbox cmdlet queries Microsoft Graph using application permissions. Provide multiple user principal names to search across several mailboxes. Results are returned as GraphMessageInfo objects.

## EXAMPLES

### EXAMPLE 1
```powershell
Search-GraphMailbox -UserPrincipalName @('Name') -Query 'Value'
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

### -From
Message index to start from.

```yaml
Type: Int32
Parameter Sets: Graph
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConcurrentRequests
Maximum number of parallel Graph requests.

```yaml
Type: Int32
Parameter Sets: Graph
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Query
Query string used to filter messages.

```yaml
Type: String
Parameter Sets: Graph
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Size
Number of messages to retrieve.

```yaml
Type: Int32
Parameter Sets: Graph
Aliases: Count
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
User principal names to search across.

```yaml
Type: String[]
Parameter Sets: Graph
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
