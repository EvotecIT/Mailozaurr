---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-GraphMailboxPermissionObject
## SYNOPSIS
Creates a GraphMailboxPermission object.

## SYNTAX
### Params
```powershell
New-GraphMailboxPermissionObject -GrantedToUser <string> [-Roles <GraphMailboxRole[]>] [-UserPrincipalName <string>] [-Id <string>] [<CommonParameters>]
```

### Builder
```powershell
New-GraphMailboxPermissionObject -Builder <GraphMailboxPermissionBuilder> [<CommonParameters>]
```

## DESCRIPTION
Creates a GraphMailboxPermission object.

## EXAMPLES

### EXAMPLE 1
```powershell
New-GraphMailboxPermissionObject -Builder 'Value'
```


### EXAMPLE 2
```powershell
New-GraphMailboxPermissionObject -GrantedToUser 'Value'
```


## PARAMETERS

### -Builder
Permission builder object used to create the permission.

```yaml
Type: GraphMailboxPermissionBuilder
Parameter Sets: Builder
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -GrantedToUser
User principal name of the grantee.

```yaml
Type: String
Parameter Sets: Params
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Id
Permission identifier.

```yaml
Type: String
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Roles
Roles assigned to the grantee.

```yaml
Type: GraphMailboxRole[]
Parameter Sets: Params
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
Parameter Sets: Params
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

- `Mailozaurr.GraphMailboxPermissionBuilder`

## OUTPUTS

- `Mailozaurr.GraphMailboxPermission`

## RELATED LINKS

- None
