---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Connect-IMAP
## SYNOPSIS
Connects to an IMAP server and authenticates using credentials, OAuth2, or clear text.

The Connect-IMAP cmdlet establishes a connection to an IMAP server using MailKit. It supports multiple authentication methods, including OAuth2, PSCredential, and clear text username/password. The cmdlet returns an ImapConnectionInfo object containing connection details and the authenticated client for further use in subsequent cmdlets.

Supports advanced options such as certificate validation skipping, custom timeouts, and secure socket options. Designed for secure, flexible, and scriptable IMAP connectivity in PowerShell automation scenarios.

## SYNTAX
### OAuth2
```powershell
Connect-IMAP [-Server <string>] [-Port <int>] [-SkipCertificateRevocation] [-SkipCertificateValidation] [-Credential <pscredential>] [-Options <SecureSocketOptions>] [-TimeOut <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-OAuth2] [<CommonParameters>]
```

### Credential
```powershell
Connect-IMAP [-Server <string>] [-Port <int>] [-SkipCertificateRevocation] [-SkipCertificateValidation] [-Credential <pscredential>] [-Options <SecureSocketOptions>] [-TimeOut <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [<CommonParameters>]
```

### ClearText
```powershell
Connect-IMAP -UserName <string> -Password <string> [-Server <string>] [-Port <int>] [-SkipCertificateRevocation] [-SkipCertificateValidation] [-Options <SecureSocketOptions>] [-TimeOut <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [<CommonParameters>]
```

## DESCRIPTION
Connects to an IMAP server and authenticates using credentials, OAuth2, or clear text.

The Connect-IMAP cmdlet establishes a connection to an IMAP server using MailKit. It supports multiple authentication methods, including OAuth2, PSCredential, and clear text username/password. The cmdlet returns an ImapConnectionInfo object containing connection details and the authenticated client for further use in subsequent cmdlets.

Supports advanced options such as certificate validation skipping, custom timeouts, and secure socket options. Designed for secure, flexible, and scriptable IMAP connectivity in PowerShell automation scenarios.

## EXAMPLES

### EXAMPLE 1
```powershell
Connect-IMAP -UserName 'Name' -Password 'Value'
```


### EXAMPLE 2
```powershell
Connect-IMAP -Credential Get-Credential
```


## PARAMETERS

### -Credential
Specifies a PSCredential object for authentication. Used for OAuth2 or standard credential-based authentication.

```yaml
Type: PSCredential
Parameter Sets: OAuth2, Credential
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OAuth2
Enables OAuth2 authentication. Use with a PSCredential object containing the access token as the password.

```yaml
Type: SwitchParameter
Parameter Sets: OAuth2
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Options
Specifies the secure socket options for the IMAP connection. Default is Auto. Options: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable.

```yaml
Type: SecureSocketOptions
Parameter Sets: OAuth2, Credential, ClearText
Aliases: None
Possible values: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
Specifies the password for clear text authentication. Required for the ClearText parameter set.

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

### -Port
Specifies the port to use for the IMAP connection. Default is 993 (IMAPS).

```yaml
Type: Int32
Parameter Sets: OAuth2, Credential, ClearText
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
Parameter Sets: OAuth2, Credential, ClearText
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
Parameter Sets: OAuth2, Credential, ClearText
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
Parameter Sets: OAuth2, Credential, ClearText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
Specifies the IMAP server hostname or IP address to connect to.

```yaml
Type: String
Parameter Sets: OAuth2, Credential, ClearText
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipCertificateRevocation
Skips certificate revocation checks during the connection. Useful for environments with limited certificate infrastructure.

```yaml
Type: SwitchParameter
Parameter Sets: OAuth2, Credential, ClearText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipCertificateValidation
Skips certificate validation. Use with caution; only for trusted/test environments or self-signed certificates.

```yaml
Type: SwitchParameter
Parameter Sets: OAuth2, Credential, ClearText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeOut
Specifies the connection timeout in milliseconds. Default is 120000 (2 minutes).

```yaml
Type: Int32
Parameter Sets: OAuth2, Credential, ClearText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserName
Specifies the username for clear text authentication. Required for the ClearText parameter set.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
- CmdletDisconnectIMAP
- CmdletGetIMAPFolder
- CmdletGetIMAPMessage
