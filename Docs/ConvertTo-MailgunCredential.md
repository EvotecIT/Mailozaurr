---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertTo-MailgunCredential
## SYNOPSIS
Creates a PSCredential object for Mailgun API authentication from an API key.

Use ConvertTo-MailgunCredential to generate a PSCredential that can be passed to Send-EmailMessage when using the Mailgun provider.

For new scripts, prefer -ApiKeySecureString or -SecretName instead of passing the API key as plain text.

## SYNTAX
### PlainText (Default)
```powershell
ConvertTo-MailgunCredential -ApiKey <string> [<CommonParameters>]
```

### SecureString
```powershell
ConvertTo-MailgunCredential -ApiKeySecureString <securestring> [<CommonParameters>]
```

### SecretManagement
```powershell
ConvertTo-MailgunCredential -SecretName <string> [-VaultName <string>] [<CommonParameters>]
```

## DESCRIPTION
Creates a PSCredential object for Mailgun API authentication from an API key.

Use ConvertTo-MailgunCredential to generate a PSCredential that can be passed to Send-EmailMessage when using the Mailgun provider.

For new scripts, prefer -ApiKeySecureString or -SecretName instead of passing the API key as plain text.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertTo-MailgunCredential -ApiKey 'Value'
```


### EXAMPLE 2
```powershell
ConvertTo-MailgunCredential -SecretName 'Name'
```


### EXAMPLE 3
```powershell
ConvertTo-MailgunCredential -ApiKeySecureString (Read-Host -AsSecureString)
```


## PARAMETERS

### -ApiKey
Mailgun API key used for authentication.

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
Mailgun API key used for authentication as a SecureString.

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
Mailgun secret name used with Get-Secret.

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

- None
