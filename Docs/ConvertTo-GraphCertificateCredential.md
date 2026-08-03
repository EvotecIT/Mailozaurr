---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertTo-GraphCertificateCredential
## SYNOPSIS
Creates a PSCredential containing a Microsoft Graph access token obtained using certificate authentication.

The ConvertTo-GraphCertificateCredential cmdlet authenticates with Microsoft Graph using a client certificate and returns a PSCredential with the access token. Use the resulting credential with cmdlets that accept Graph tokens.

For new scripts, prefer -CertificatePasswordSecureString or -SecretName rather than passing the certificate password as plain text.

## SYNTAX
### PlainText (Default)
```powershell
ConvertTo-GraphCertificateCredential -ClientId <string> -TenantId <string> -CertificatePath <string> -CertificatePassword <string> [-Scopes <string[]>] [<CommonParameters>]
```

### SecureString
```powershell
ConvertTo-GraphCertificateCredential -ClientId <string> -TenantId <string> -CertificatePath <string> -CertificatePasswordSecureString <securestring> [-Scopes <string[]>] [<CommonParameters>]
```

### SecretManagement
```powershell
ConvertTo-GraphCertificateCredential -ClientId <string> -TenantId <string> -CertificatePath <string> -SecretName <string> [-VaultName <string>] [-Scopes <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Creates a PSCredential containing a Microsoft Graph access token obtained using certificate authentication.

The ConvertTo-GraphCertificateCredential cmdlet authenticates with Microsoft Graph using a client certificate and returns a PSCredential with the access token. Use the resulting credential with cmdlets that accept Graph tokens.

For new scripts, prefer -CertificatePasswordSecureString or -SecretName rather than passing the certificate password as plain text.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertTo-GraphCertificateCredential -ClientId 'Value' -TenantId 'Value' -CertificatePath 'C:\Path' -CertificatePassword 'Value'
```


### EXAMPLE 2
```powershell
ConvertTo-GraphCertificateCredential -ClientId 'Value' -TenantId 'Value' -CertificatePath 'C:\Path' -SecretName 'Name'
```


### EXAMPLE 3
```powershell
ConvertTo-GraphCertificateCredential -ClientId 'Value' -TenantId 'Value' -CertificatePath 'C:\Path' -CertificatePasswordSecureString (Read-Host -AsSecureString)
```


## PARAMETERS

### -CertificatePassword
Password for the client certificate.

```yaml
Type: String
Parameter Sets: PlainText
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePasswordSecureString
Password for the client certificate as a SecureString.

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

### -CertificatePath
Path to the client certificate (PFX).

```yaml
Type: String
Parameter Sets: PlainText, SecureString, SecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientId
Azure AD application (client) identifier.

```yaml
Type: String
Parameter Sets: PlainText, SecureString, SecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scopes
Optional scopes to request.

```yaml
Type: String[]
Parameter Sets: PlainText, SecureString, SecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecretName
Name of the certificate password secret resolved via Get-Secret.

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

### -TenantId
Azure AD tenant identifier.

```yaml
Type: String
Parameter Sets: PlainText, SecureString, SecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VaultName
Optional vault name used with Get-Secret.

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

- None
