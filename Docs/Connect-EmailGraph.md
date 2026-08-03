---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Connect-EmailGraph
## SYNOPSIS
Connects to Microsoft Graph using application credentials, certificates, device code, or on-behalf-of authentication.

The Connect-EmailGraph cmdlet creates a Microsoft Graph connection for Mailozaurr cmdlets. It supports client secret, certificate, device code, and on-behalf-of flows, and can authenticate directly or from a prebuilt PSCredential.

For new scripts, prefer the SecureString or SecretManagement parameter sets instead of passing secrets and tokens in plain text.

## SYNTAX
### Credential
```powershell
Connect-EmailGraph -Credential <pscredential> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### Plain
```powershell
Connect-EmailGraph -ClientId <string> -ClientSecret <string> -DirectoryId <string> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### Certificate
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -CertificatePath <string> -CertificatePassword <string> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### CertificateBytes
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -CertificateBytes <byte[]> -CertificatePassword <string> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### CertificatePem
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -CertificatePemPath <string> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### DeviceCode
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -DeviceCode [-Scopes <string[]>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### OnBehalfOf
```powershell
Connect-EmailGraph -ClientId <string> -ClientSecret <string> -DirectoryId <string> -OnBehalfOfToken <string> [-Scopes <string[]>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### PlainSecureString
```powershell
Connect-EmailGraph -ClientId <string> -ClientSecretSecureString <securestring> -DirectoryId <string> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### CertificateSecureString
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -CertificatePath <string> -CertificatePasswordSecureString <securestring> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### CertificateBytesSecureString
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -CertificateBytes <byte[]> -CertificatePasswordSecureString <securestring> [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### OnBehalfOfSecureString
```powershell
Connect-EmailGraph -ClientId <string> -ClientSecretSecureString <securestring> -DirectoryId <string> -OnBehalfOfTokenSecureString <securestring> [-Scopes <string[]>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### PlainSecretManagement
```powershell
Connect-EmailGraph -ClientId <string> -ClientSecretSecretName <string> -DirectoryId <string> [-ClientSecretVaultName <string>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### CertificateSecretManagement
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -CertificatePath <string> -CertificatePasswordSecretName <string> [-CertificatePasswordVaultName <string>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### CertificateBytesSecretManagement
```powershell
Connect-EmailGraph -ClientId <string> -DirectoryId <string> -CertificateBytes <byte[]> -CertificatePasswordSecretName <string> [-CertificatePasswordVaultName <string>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

### OnBehalfOfSecretManagement
```powershell
Connect-EmailGraph -ClientId <string> -ClientSecretSecretName <string> -DirectoryId <string> -OnBehalfOfTokenSecretName <string> [-ClientSecretVaultName <string>] [-OnBehalfOfTokenVaultName <string>] [-Scopes <string[]>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-TimeoutSeconds <int>] [-MaxConcurrentRequests <int>] [<CommonParameters>]
```

## DESCRIPTION
Connects to Microsoft Graph using application credentials, certificates, device code, or on-behalf-of authentication.

The Connect-EmailGraph cmdlet creates a Microsoft Graph connection for Mailozaurr cmdlets. It supports client secret, certificate, device code, and on-behalf-of flows, and can authenticate directly or from a prebuilt PSCredential.

For new scripts, prefer the SecureString or SecretManagement parameter sets instead of passing secrets and tokens in plain text.

## EXAMPLES

### EXAMPLE 1
```powershell
Connect-EmailGraph -ClientId 'Value' -DirectoryId 'Value' -CertificatePath 'C:\Path' -CertificatePassword 'Value'
```


### EXAMPLE 2
```powershell
Connect-EmailGraph -ClientId 'Value' -DirectoryId 'Value' -CertificateBytes @('Value') -CertificatePassword 'Value'
```


### EXAMPLE 3
```powershell
Connect-EmailGraph -ClientId 'Value' -DirectoryId 'Value' -CertificateBytes @('Value') -CertificatePasswordSecretName 'Name'
```


## PARAMETERS

### -CertificateBytes
Raw bytes of a PFX certificate used for authentication.

```yaml
Type: Byte[]
Parameter Sets: CertificateBytes, CertificateBytesSecureString, CertificateBytesSecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePassword
Password used to decrypt the certificate file.

```yaml
Type: String
Parameter Sets: Certificate, CertificateBytes
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePasswordSecretName
Secret name used with Get-Secret for the certificate password.

```yaml
Type: String
Parameter Sets: CertificateSecretManagement, CertificateBytesSecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePasswordSecureString
Password used to decrypt the certificate file as a SecureString.

```yaml
Type: SecureString
Parameter Sets: CertificateSecureString, CertificateBytesSecureString
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePasswordVaultName
Optional vault name used with Get-Secret for the certificate password.

```yaml
Type: String
Parameter Sets: CertificateSecretManagement, CertificateBytesSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePath
Path to a PFX certificate used for authentication.

```yaml
Type: String
Parameter Sets: Certificate, CertificateSecureString, CertificateSecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePemPath
Path to a PEM encoded certificate used for authentication.

```yaml
Type: String
Parameter Sets: CertificatePem
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientId
Client (application) identifier.

```yaml
Type: String
Parameter Sets: Plain, Certificate, CertificateBytes, CertificatePem, DeviceCode, OnBehalfOf, PlainSecureString, CertificateSecureString, CertificateBytesSecureString, OnBehalfOfSecureString, PlainSecretManagement, CertificateSecretManagement, CertificateBytesSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecret
Secret associated with the application (for app-only auth).

```yaml
Type: String
Parameter Sets: Plain, OnBehalfOf
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecretSecretName
Secret name used with Get-Secret for the application client secret.

```yaml
Type: String
Parameter Sets: PlainSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecretSecureString
Secret associated with the application (for app-only auth) as a SecureString.

```yaml
Type: SecureString
Parameter Sets: PlainSecureString, OnBehalfOfSecureString
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecretVaultName
Optional vault name used with Get-Secret for the application client secret.

```yaml
Type: String
Parameter Sets: PlainSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credential object containing client ID and secret.

```yaml
Type: PSCredential
Parameter Sets: Credential
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeviceCode
Use the device code flow for authentication.

```yaml
Type: SwitchParameter
Parameter Sets: DeviceCode
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DirectoryId
Directory (tenant) identifier.

```yaml
Type: String
Parameter Sets: Plain, Certificate, CertificateBytes, CertificatePem, DeviceCode, OnBehalfOf, PlainSecureString, CertificateSecureString, CertificateBytesSecureString, OnBehalfOfSecureString, PlainSecretManagement, CertificateSecretManagement, CertificateBytesSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxConcurrentRequests
Maximum number of concurrent Microsoft Graph requests.

```yaml
Type: Int32
Parameter Sets: Credential, Plain, Certificate, CertificateBytes, CertificatePem, DeviceCode, OnBehalfOf, PlainSecureString, CertificateSecureString, CertificateBytesSecureString, OnBehalfOfSecureString, PlainSecretManagement, CertificateSecretManagement, CertificateBytesSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnBehalfOfToken
Access token to use for on-behalf-of authentication.

```yaml
Type: String
Parameter Sets: OnBehalfOf
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnBehalfOfTokenSecretName
Secret name used with Get-Secret for the on-behalf-of token.

```yaml
Type: String
Parameter Sets: OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnBehalfOfTokenSecureString
Access token to use for on-behalf-of authentication as a SecureString.

```yaml
Type: SecureString
Parameter Sets: OnBehalfOfSecureString
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnBehalfOfTokenVaultName
Optional vault name used with Get-Secret for the on-behalf-of token.

```yaml
Type: String
Parameter Sets: OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryCount
Number of connection retry attempts.

```yaml
Type: Int32
Parameter Sets: Credential, Plain, Certificate, CertificateBytes, CertificatePem, DeviceCode, OnBehalfOf, PlainSecureString, CertificateSecureString, CertificateBytesSecureString, OnBehalfOfSecureString, PlainSecretManagement, CertificateSecretManagement, CertificateBytesSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryDelayBackoff
Multiplier for increasing retry delay.

```yaml
Type: Double
Parameter Sets: Credential, Plain, Certificate, CertificateBytes, CertificatePem, DeviceCode, OnBehalfOf, PlainSecureString, CertificateSecureString, CertificateBytesSecureString, OnBehalfOfSecureString, PlainSecretManagement, CertificateSecretManagement, CertificateBytesSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryDelayMilliseconds
Delay in milliseconds between retries.

```yaml
Type: Int32
Parameter Sets: Credential, Plain, Certificate, CertificateBytes, CertificatePem, DeviceCode, OnBehalfOf, PlainSecureString, CertificateSecureString, CertificateBytesSecureString, OnBehalfOfSecureString, PlainSecretManagement, CertificateSecretManagement, CertificateBytesSecretManagement, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scopes
Microsoft Graph permission scopes to request.

```yaml
Type: String[]
Parameter Sets: DeviceCode, OnBehalfOf, OnBehalfOfSecureString, OnBehalfOfSecretManagement
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutSeconds
Request timeout for Microsoft Graph operations in seconds.

```yaml
Type: Int32
Parameter Sets: Credential, Plain, Certificate, CertificateBytes, CertificatePem, DeviceCode, OnBehalfOf, PlainSecureString, CertificateSecureString, CertificateBytesSecureString, OnBehalfOfSecureString, PlainSecretManagement, CertificateSecretManagement, CertificateBytesSecretManagement, OnBehalfOfSecretManagement
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

- `Mailozaurr.PowerShell.GraphConnectionInfo`: Represents an authenticated Microsoft Graph connection.

## RELATED LINKS

- None
