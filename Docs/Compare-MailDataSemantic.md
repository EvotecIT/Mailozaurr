---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Compare-MailDataSemantic
## SYNOPSIS
Compares two email documents by their semantic content.

Returns the native OfficeIMO.Email difference report. Import both artifacts first and keep their results open until comparison finishes.

## SYNTAX
### __AllParameterSets
```powershell
Compare-MailDataSemantic [-InputObject] <Object> [-ReferenceObject] <Object> [-Options <EmailSemanticComparisonOptions>] [<CommonParameters>]
```

## DESCRIPTION
Compares two email documents by their semantic content.

Returns the native OfficeIMO.Email difference report. Import both artifacts first and keep their results open until comparison finishes.

## EXAMPLES

### EXAMPLE 1
```powershell
Compare-MailDataSemantic -InputObject 'Value'
```


## PARAMETERS

### -InputObject
Source email or Import-MailData result.

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

### -ReferenceObject
Destination email or Import-MailData result.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `OfficeIMO.Email.EmailSemanticComparisonReport`

## RELATED LINKS

- None
