---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-GraphMailboxPermissionBuilder
## SYNOPSIS
Creates a GraphMailboxPermissionBuilder instance.

## SYNTAX
### __AllParameterSets
```powershell
New-GraphMailboxPermissionBuilder -GrantedToUser <string> [-UserPrincipalName <string>] [-Roles <GraphMailboxRole[]>] [-Id <string>] [<CommonParameters>]
```

## DESCRIPTION
Creates a GraphMailboxPermissionBuilder instance.

## EXAMPLES

### EXAMPLE 1
```powershell
New-GraphMailboxPermissionBuilder -GrantedToUser 'Value'
```


## PARAMETERS

### -GrantedToUser
User to grant permissions to.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Id
Optional permission identifier.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Roles
Roles to assign on the mailbox.

```yaml
Type: GraphMailboxRole[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Owner, Read, Write, Custom

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
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
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

- `Mailozaurr.GraphMailboxPermissionBuilder`

## RELATED LINKS

- None
