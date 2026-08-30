---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Set-MailProfileSecret
## SYNOPSIS
Stores or copies a protected secret for a saved mail profile.

## SYNTAX
### Value (Default)
```powershell
Set-MailProfileSecret [-ProfileId] <string> [-Name] <string> -Value <securestring> [-ProfileDirectory <string>] [-SecretDirectory <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Reference
```powershell
Set-MailProfileSecret [-ProfileId] <string> [-Name] <string> -Reference <string> [-AllowCrossProfileReference] [-ProfileDirectory <string>] [-SecretDirectory <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Stores or copies a protected secret for a saved mail profile.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-MailProfileSecret -Value (Read-Host -AsSecureString)
```


### EXAMPLE 2
```powershell
Set-MailProfileSecret -Reference 'Value'
```


## PARAMETERS

### -AllowCrossProfileReference
Compatibility switch retained for scripts. Cross-profile references remain rejected until
Mailozaurr can enforce typed provider, tenant, client, origin, and purpose metadata.

```yaml
Type: SwitchParameter
Parameter Sets: Reference
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Well-known or provider-specific secret name.

```yaml
Type: String
Parameter Sets: Value, Reference
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProfileDirectory
Optional directory containing the profile store.

```yaml
Type: String
Parameter Sets: Value, Reference
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProfileId
Profile identifier.

```yaml
Type: String
Parameter Sets: Value, Reference
Aliases: Id
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Reference
Stored secret reference in profile-id:secret-name form.

```yaml
Type: String
Parameter Sets: Reference
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecretDirectory
Optional directory containing the protected secret store.

```yaml
Type: String
Parameter Sets: Value, Reference
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Value
Secret value supplied through PowerShell's protected string type.

```yaml
Type: SecureString
Parameter Sets: Value
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

- `Mailozaurr.OperationResult`

## RELATED LINKS

- None
