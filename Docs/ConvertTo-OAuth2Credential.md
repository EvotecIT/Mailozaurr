---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertTo-OAuth2Credential
## SYNOPSIS
Creates a PSCredential object for OAuth2 authentication from a username and token.

The ConvertTo-OAuth2Credential cmdlet creates a PSCredential object suitable for OAuth2 authentication, using the provided username and access token. The resulting credential can be used with cmdlets that require OAuth2 authentication (such as IMAP, SMTP, or POP3 with OAuth2).

For automation and interactive use, prefer -TokenSecureString or -SecretName over passing the token as plain text.

## SYNTAX
### PlainText (Default)
```powershell
ConvertTo-OAuth2Credential -UserName <string> -Token <string> [<CommonParameters>]
```

### SecureString
```powershell
ConvertTo-OAuth2Credential -UserName <string> -TokenSecureString <securestring> [<CommonParameters>]
```

### SecretManagement
```powershell
ConvertTo-OAuth2Credential -UserName <string> -SecretName <string> [-VaultName <string>] [<CommonParameters>]
```

## DESCRIPTION
Creates a PSCredential object for OAuth2 authentication from a username and token.

The ConvertTo-OAuth2Credential cmdlet creates a PSCredential object suitable for OAuth2 authentication, using the provided username and access token. The resulting credential can be used with cmdlets that require OAuth2 authentication (such as IMAP, SMTP, or POP3 with OAuth2).

For automation and interactive use, prefer -TokenSecureString or -SecretName over passing the token as plain text.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertTo-OAuth2Credential -UserName 'Name' -Token 'Value'
```


### EXAMPLE 2
```powershell
ConvertTo-OAuth2Credential -UserName 'Name' -SecretName 'Name'
```


### EXAMPLE 3
```powershell
ConvertTo-OAuth2Credential -UserName 'Name' -TokenSecureString (Read-Host -AsSecureString)
```


## PARAMETERS

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

### -Token
Specifies the OAuth2 access token.

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

### -TokenSecureString
Specifies the OAuth2 access token as a SecureString.

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

### -UserName
Specifies the username for OAuth2 authentication.

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

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
