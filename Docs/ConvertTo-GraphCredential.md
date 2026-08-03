---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertTo-GraphCredential
## SYNOPSIS
Creates a PSCredential object for Microsoft Graph authentication from client ID, secret, and directory ID.

The ConvertTo-GraphCredential cmdlet creates a PSCredential object suitable for Microsoft Graph authentication, using the provided client ID, client secret, and directory (tenant) ID. The resulting credential can be used with cmdlets that require Graph authentication.

For new scripts, prefer -ClientSecretSecureString or -SecretName instead of passing the client secret in plain text.

## SYNTAX
### ClearText (Default)
```powershell
ConvertTo-GraphCredential -ClientId <string> -ClientSecret <string> -DirectoryId <string> [<CommonParameters>]
```

### Encrypted
```powershell
ConvertTo-GraphCredential -ClientId <string> -ClientSecretEncrypted <string> -DirectoryId <string> [<CommonParameters>]
```

### SecureString
```powershell
ConvertTo-GraphCredential -ClientId <string> -ClientSecretSecureString <securestring> -DirectoryId <string> [<CommonParameters>]
```

### SecretManagement
```powershell
ConvertTo-GraphCredential -ClientId <string> -SecretName <string> -DirectoryId <string> [-VaultName <string>] [<CommonParameters>]
```

## DESCRIPTION
Creates a PSCredential object for Microsoft Graph authentication from client ID, secret, and directory ID.

The ConvertTo-GraphCredential cmdlet creates a PSCredential object suitable for Microsoft Graph authentication, using the provided client ID, client secret, and directory (tenant) ID. The resulting credential can be used with cmdlets that require Graph authentication.

For new scripts, prefer -ClientSecretSecureString or -SecretName instead of passing the client secret in plain text.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertTo-GraphCredential -ClientId 'Value' -ClientSecret 'Value' -DirectoryId 'Value'
```


### EXAMPLE 2
```powershell
ConvertTo-GraphCredential -ClientId 'Value' -ClientSecretEncrypted 'Value' -DirectoryId 'Value'
```


### EXAMPLE 3
```powershell
ConvertTo-GraphCredential -ClientId 'Value' -SecretName 'Name' -DirectoryId 'Value'
```


## PARAMETERS

### -ClientId
Specifies the client ID for Microsoft Graph authentication.

```yaml
Type: String
Parameter Sets: ClearText, Encrypted, SecureString, SecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecret
Specifies the client secret in clear text. Use only with the ClearText parameter set.

```yaml
Type: String
Parameter Sets: ClearText
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecretEncrypted
Specifies the client secret in encrypted form. Use only with the Encrypted parameter set.

```yaml
Type: String
Parameter Sets: Encrypted
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecretSecureString
Specifies the client secret as a SecureString. Use only with the SecureString parameter set.

```yaml
Type: SecureString
Parameter Sets: SecureString
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DirectoryId
Specifies the directory (tenant) ID for Microsoft Graph authentication.

```yaml
Type: String
Parameter Sets: ClearText, Encrypted, SecureString, SecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecretName
Specifies the name of a secret to resolve using Get-Secret. Use only with the SecretManagement parameter set.

```yaml
Type: String
Parameter Sets: SecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VaultName
Optional vault name used with Get-Secret. Use only with the SecretManagement parameter set.

```yaml
Type: String
Parameter Sets: SecretManagement
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

- `System.Management.Automation.PSCredential`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
