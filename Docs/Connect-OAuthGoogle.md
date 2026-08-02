---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Connect-OAuthGoogle
## SYNOPSIS
Obtains an OAuth2 access token for a Google (Gmail) account for use with IMAP, SMTP, or other Google APIs.

The Connect-OAuthGoogle cmdlet initiates an interactive OAuth2 authentication flow for a Gmail account, returning a PSCredential object containing the access token. This credential can be used with other cmdlets (such as Connect-IMAP) that support OAuth2 authentication.

For new scripts, prefer -ClientSecretSecureString or -ClientSecretSecretName rather than passing the client secret as plain text.

## SYNTAX
### PlainText (Default)
```powershell
Connect-OAuthGoogle -GmailAccount <string> -ClientID <string> -ClientSecret <string> [-Scope <string[]>] [<CommonParameters>]
```

### SecureString
```powershell
Connect-OAuthGoogle -GmailAccount <string> -ClientID <string> -ClientSecretSecureString <securestring> [-Scope <string[]>] [<CommonParameters>]
```

### SecretManagement
```powershell
Connect-OAuthGoogle -GmailAccount <string> -ClientID <string> -ClientSecretSecretName <string> [-ClientSecretVaultName <string>] [-Scope <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Obtains an OAuth2 access token for a Google (Gmail) account for use with IMAP, SMTP, or other Google APIs.

The Connect-OAuthGoogle cmdlet initiates an interactive OAuth2 authentication flow for a Gmail account, returning a PSCredential object containing the access token. This credential can be used with other cmdlets (such as Connect-IMAP) that support OAuth2 authentication.

For new scripts, prefer -ClientSecretSecureString or -ClientSecretSecretName rather than passing the client secret as plain text.

## EXAMPLES

### EXAMPLE 1
```powershell
Connect-OAuthGoogle -GmailAccount 'Value' -ClientID 'Value' -ClientSecret 'Value'
```


### EXAMPLE 2
```powershell
Connect-OAuthGoogle -GmailAccount 'Value' -ClientID 'Value' -ClientSecretSecretName 'Name'
```


### EXAMPLE 3
```powershell
Connect-OAuthGoogle -GmailAccount 'Value' -ClientID 'Value' -ClientSecretSecureString (Read-Host -AsSecureString)
```


## PARAMETERS

### -ClientID
Specifies the OAuth2 client ID from the Google Developer Console.

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

### -ClientSecret
Specifies the OAuth2 client secret from the Google Developer Console.

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

### -ClientSecretSecretName
Specifies the name of a secret to resolve using Get-Secret for the OAuth2 client secret.

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

### -ClientSecretSecureString
Specifies the OAuth2 client secret from the Google Developer Console as a SecureString.

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

### -ClientSecretVaultName
Optional vault name used with Get-Secret for the OAuth2 client secret.

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

### -GmailAccount
Specifies the Gmail account (email address) to authenticate.

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

### -Scope
Specifies the OAuth2 scopes to request. Default is "https://mail.google.com/".

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `System.Management.Automation.PSCredential`

## RELATED LINKS

- CmdletConnectIMAP
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
