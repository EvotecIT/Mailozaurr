---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-EmailGraphMessageMime
## SYNOPSIS
Retrieves a MIME representation of a Graph mail message.

## SYNTAX
### Info
```powershell
Get-EmailGraphMessageMime -MessageInfo <GraphMessageInfo> [-Connection <GraphConnectionInfo>] [<CommonParameters>]
```

### ById
```powershell
Get-EmailGraphMessageMime -UserPrincipalName <string> -MessageId <string> [-Connection <GraphConnectionInfo>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves a MIME representation of a Graph mail message.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EmailGraphMessageMime -UserPrincipalName 'Name' -MessageId 'Value'
```


### EXAMPLE 2
```powershell
Get-EmailGraphMessageMime -MessageInfo 'Value'
```


## PARAMETERS

### -Connection
Graph connection.

```yaml
Type: GraphConnectionInfo
Parameter Sets: Info, ById
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageId
Identifier of the message.

```yaml
Type: String
Parameter Sets: ById
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageInfo
Message info from Get-EmailGraphMessage.

```yaml
Type: GraphMessageInfo
Parameter Sets: Info
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -UserPrincipalName
User principal name owning the message.

```yaml
Type: String
Parameter Sets: ById
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

- `Mailozaurr.GraphMessageInfo`

## OUTPUTS

- `Mailozaurr.GraphEmailMessage`

## RELATED LINKS

- None
