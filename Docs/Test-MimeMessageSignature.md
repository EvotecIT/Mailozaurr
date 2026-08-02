---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Test-MimeMessageSignature
## SYNOPSIS
Verifies PGP or S/MIME signatures on a MimeMessage.

## SYNTAX
### Auto (Default)
```powershell
Test-MimeMessageSignature -InputObject <Object> [<CommonParameters>]
```

### Pgp
```powershell
Test-MimeMessageSignature -InputObject <Object> [-PublicKeyPath <string>] [<CommonParameters>]
```

### Smime
```powershell
Test-MimeMessageSignature -InputObject <Object> [-Certificate <X509Certificate2[]>] [<CommonParameters>]
```

## DESCRIPTION
Verifies PGP or S/MIME signatures on a MimeMessage.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-MimeMessageSignature -InputObject 'Value'
```


## PARAMETERS

### -Certificate
Certificates for S/MIME signature verification.

```yaml
Type: X509Certificate2[]
Parameter Sets: Smime
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Message to verify.

```yaml
Type: Object
Parameter Sets: Auto, Pgp, Smime
Aliases: Message
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -PublicKeyPath
Public key for PGP signature verification.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `System.Boolean`

## RELATED LINKS

- None
