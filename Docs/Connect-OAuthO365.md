---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Connect-OAuthO365
## SYNOPSIS
Obtains an OAuth2 access token for an Office 365 (Microsoft 365) account for use with IMAP, SMTP, or Microsoft Graph.

The Connect-OAuthO365 cmdlet initiates an interactive OAuth2 authentication flow for an Office 365 account, returning a PSCredential object containing the access token. This credential can be used with other cmdlets (such as Connect-IMAP or Send-EmailMessage) that support OAuth2 authentication.

## SYNTAX
### __AllParameterSets
```powershell
Connect-OAuthO365 -ClientID <string> -TenantID <string> [-Login <string>] [-RedirectUri <string>] [-Scopes <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Obtains an OAuth2 access token for an Office 365 (Microsoft 365) account for use with IMAP, SMTP, or Microsoft Graph.

The Connect-OAuthO365 cmdlet initiates an interactive OAuth2 authentication flow for an Office 365 account, returning a PSCredential object containing the access token. This credential can be used with other cmdlets (such as Connect-IMAP or Send-EmailMessage) that support OAuth2 authentication.

## EXAMPLES

### EXAMPLE 1
```powershell
Connect-OAuthO365 -ClientID 'Value' -TenantID 'Value'
```


## PARAMETERS

### -ClientID
Specifies the OAuth2 client ID from Azure AD App Registration.

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

### -Login
Specifies the login (user principal name) for the Office 365 account. Optional; if not provided, interactive login is used.

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

### -RedirectUri
Specifies the redirect URI for the OAuth2 flow. Default is the recommended Microsoft URI.

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

### -Scopes
Specifies the OAuth2 scopes to request. Default includes IMAP, POP, and SMTP permissions.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TenantID
Specifies the Azure AD tenant ID (Directory ID).

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `System.Management.Automation.PSCredential`

## RELATED LINKS

- CmdletConnectIMAP
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
