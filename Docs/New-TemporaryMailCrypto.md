---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-TemporaryMailCrypto
## SYNOPSIS
Creates temporary cryptographic material for testing mail encryption.

## SYNTAX
### Pgp
```powershell
New-TemporaryMailCrypto -Pgp [-Identity <string>] [-PassPhrase <string>] [-KeySize <int>] [-OutputPath <string>] [-NoDispose] [<CommonParameters>]
```

### Smime
```powershell
New-TemporaryMailCrypto -Smime [-OutputPath <string>] [-NoDispose] [-SubjectName <string>] [-ValidDays <int>] [-OutputPassword <securestring>] [<CommonParameters>]
```

## DESCRIPTION
Creates temporary cryptographic material for testing mail encryption.

## EXAMPLES

### EXAMPLE 1
```powershell
New-TemporaryMailCrypto -Pgp
```


### EXAMPLE 2
```powershell
New-TemporaryMailCrypto -Smime
```


## PARAMETERS

### -Identity
Identity for the PGP key.

```yaml
Type: String
Parameter Sets: Pgp
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -KeySize
Size of the RSA key.

```yaml
Type: Int32
Parameter Sets: Pgp
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoDispose
Do not delete generated files when disposed.

```yaml
Type: SwitchParameter
Parameter Sets: Pgp, Smime
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPassword
Password protecting an exported S/MIME PFX file.

```yaml
Type: SecureString
Parameter Sets: Smime
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Optional path where the generated data should be stored.

```yaml
Type: String
Parameter Sets: Pgp, Smime
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassPhrase
Passphrase for the private key.

```yaml
Type: String
Parameter Sets: Pgp
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Pgp
Generate a PGP key pair.

```yaml
Type: SwitchParameter
Parameter Sets: Pgp
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Smime
Generate an S/MIME certificate.

```yaml
Type: SwitchParameter
Parameter Sets: Smime
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubjectName
Subject for the certificate.

```yaml
Type: String
Parameter Sets: Smime
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ValidDays
Number of days the certificate is valid.

```yaml
Type: Int32
Parameter Sets: Smime
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

- `Mailozaurr.TemporaryPgpKeyPair`
- `System.Security.Cryptography.X509Certificates.X509Certificate2`

## RELATED LINKS

- None
