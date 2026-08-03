---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertTo-SendGridCredential
## SYNOPSIS
Creates a PSCredential object for SendGrid API authentication from an API key.

The ConvertTo-SendGridCredential cmdlet creates a PSCredential object suitable for SendGrid API authentication, using the provided API key. The resulting credential can be used with cmdlets that require SendGrid authentication (such as Send-EmailMessage with the -SendGrid switch).

For new scripts, prefer -ApiKeySecureString or -SecretName instead of passing the API key as plain text.

## SYNTAX
### PlainText (Default)
```powershell
ConvertTo-SendGridCredential -ApiKey <string> [<CommonParameters>]
```

### SecureString
```powershell
ConvertTo-SendGridCredential -ApiKeySecureString <securestring> [<CommonParameters>]
```

### SecretManagement
```powershell
ConvertTo-SendGridCredential -SecretName <string> [-VaultName <string>] [<CommonParameters>]
```

## DESCRIPTION
Creates a PSCredential object for SendGrid API authentication from an API key.

The ConvertTo-SendGridCredential cmdlet creates a PSCredential object suitable for SendGrid API authentication, using the provided API key. The resulting credential can be used with cmdlets that require SendGrid authentication (such as Send-EmailMessage with the -SendGrid switch).

For new scripts, prefer -ApiKeySecureString or -SecretName instead of passing the API key as plain text.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertTo-SendGridCredential -ApiKey 'Value'
```


### EXAMPLE 2
```powershell
ConvertTo-SendGridCredential -SecretName 'Name'
```


### EXAMPLE 3
```powershell
ConvertTo-SendGridCredential -ApiKeySecureString (Read-Host -AsSecureString)
```


## PARAMETERS

### -ApiKey
Specifies the SendGrid API key.

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

### -ApiKeySecureString
Specifies the SendGrid API key as a SecureString.

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

### -SecretName
Specifies the name of a secret to resolve using Get-Secret.

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

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
