---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Test-MailProfile
## SYNOPSIS
Diagnoses stored profile readiness or performs a live connection probe.

## SYNTAX
### __AllParameterSets
```powershell
Test-MailProfile [-ProfileId] <string> [-Connection] [-Scope <MailProfileConnectionTestScope>] [-ProfileDirectory <string>] [-SecretDirectory <string>] [<CommonParameters>]
```

## DESCRIPTION
Diagnoses stored profile readiness or performs a live connection probe.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-MailProfile -Connection
```


## PARAMETERS

### -Connection
Runs a live provider connection probe instead of stored readiness diagnosis.

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
Profile identifier.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Id
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -Scope
Depth of the live connection probe.

```yaml
Type: MailProfileConnectionTestScope
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Auto, Auth, Mailbox, Send

Required: False
Position: named
Default value: None
Accept pipeline input: False
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

- `Mailozaurr.MailProfileValidationResult`
- `Mailozaurr.MailProfileConnectionTestResult`

## RELATED LINKS

- None
