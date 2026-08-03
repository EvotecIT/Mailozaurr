---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Test-EmailAddress
## SYNOPSIS
Validates one or more email addresses for format and standards compliance.

The Test-EmailAddress cmdlet checks if one or more email addresses are valid according to standard email address rules. Supports validation for international addresses and top-level domains. Returns validation results for each address.

## SYNTAX
### __AllParameterSets
```powershell
Test-EmailAddress [-EmailAddress] <string[]> [-AllowInternational] [-AllowTopLevelDomains] [<CommonParameters>]
```

## DESCRIPTION
Validates one or more email addresses for format and standards compliance.

The Test-EmailAddress cmdlet checks if one or more email addresses are valid according to standard email address rules. Supports validation for international addresses and top-level domains. Returns validation results for each address.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EmailAddress -AllowInternational
```


## PARAMETERS

### -AllowInternational
If set, the cmdlet will use the newer international email standards to validate the email addresses.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowTopLevelDomains
If set, the cmdlet will allow top level domains in the email addresses (such as test@email).

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EmailAddress
Specifies the email addresses to check. Accepts an array of strings. This parameter is mandatory.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String[]`

## OUTPUTS

- `None`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
