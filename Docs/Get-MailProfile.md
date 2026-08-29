---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailProfile
## SYNOPSIS
Gets one or more saved Mailozaurr profiles without exposing secret values.

## SYNTAX
### __AllParameterSets
```powershell
Get-MailProfile [[-ProfileId] <string>] [-Kind <MailProfileKind>] [-DefaultOnly] [-ProfileDirectory <string>] [-SecretDirectory <string>] [<CommonParameters>]
```

## DESCRIPTION
Gets one or more saved Mailozaurr profiles without exposing secret values.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailProfile -DefaultOnly
```


## PARAMETERS

### -DefaultOnly
Returns only the default profile.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Kind
Optional provider kind filter when listing profiles.

```yaml
Type: MailProfileKind
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Unknown, Imap, Pop3, Graph, Gmail, Smtp, SendGrid, Mailgun, Ses, Jmap

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProfileDirectory
Optional directory containing the profile store.

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

### -ProfileId
Optional profile identifier. All profiles are returned when omitted.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Id
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -SecretDirectory
Optional directory containing the protected secret store.

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

- `System.String`

## OUTPUTS

- `Mailozaurr.MailProfile`

## RELATED LINKS

- None
