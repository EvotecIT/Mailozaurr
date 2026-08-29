---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-JMAPIdentity
## SYNOPSIS
Lists JMAP sending identities when submission is available.

## SYNTAX
### __AllParameterSets
```powershell
Get-JMAPIdentity [-ProfileId] <string> [-ProfileDirectory <string>] [-SecretDirectory <string>] [<CommonParameters>]
```

## DESCRIPTION
Lists JMAP sending identities when submission is available.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-JMAPIdentity -ProfileDirectory 'Value'
```


## PARAMETERS

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
JMAP profile identifier.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Id
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
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

- `Mailozaurr.JmapIdentity`

## RELATED LINKS

- None
