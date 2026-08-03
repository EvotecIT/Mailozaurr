---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Unprotect-MimeMessage
## SYNOPSIS
Decrypts an encrypted MimeMessage using PGP or S/MIME.

## SYNTAX
### __AllParameterSets
```powershell
Unprotect-MimeMessage [-InputObject] <Object> [-PrivateKeyPath <string>] [-PrivateKeyPassword <string>] [-Certificate <X509Certificate2>] [<CommonParameters>]
```

## DESCRIPTION
Decrypts an encrypted MimeMessage using PGP or S/MIME.

## EXAMPLES

### EXAMPLE 1
```powershell
Unprotect-MimeMessage -PrivateKeyPath 'C:\Path'
```


## PARAMETERS

### -Certificate
Certificate for S/MIME decryption.

```yaml
Type: X509Certificate2
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Object containing the MIME message.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: Message
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -PrivateKeyPassword
Password for PGP private key.

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

### -PrivateKeyPath
Path to PGP private key.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `MimeKit.MimeMessage`

## RELATED LINKS

- None
