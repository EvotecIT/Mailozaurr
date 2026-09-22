---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailSemanticFingerprint
## SYNOPSIS
Computes a semantic fingerprint of an email document.

Uses OfficeIMO.Email to fingerprint normalized content and attachments. Supply keyed digest options before persisting fingerprints of private mail.

## SYNTAX
### __AllParameterSets
```powershell
Get-MailSemanticFingerprint [-InputObject] <Object> [-Options <EmailSemanticComparisonOptions>] [<CommonParameters>]
```

## DESCRIPTION
Computes a semantic fingerprint of an email document.

Uses OfficeIMO.Email to fingerprint normalized content and attachments. Supply keyed digest options before persisting fingerprints of private mail.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailSemanticFingerprint -InputObject 'Value'
```


## PARAMETERS

### -InputObject
Email or Import-MailData result.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Options
Optional OfficeIMO semantic comparison policy.

```yaml
Type: EmailSemanticComparisonOptions
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

- `OfficeIMO.Email.EmailSemanticFingerprint`

## RELATED LINKS

- None
