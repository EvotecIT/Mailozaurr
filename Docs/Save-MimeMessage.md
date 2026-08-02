---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-MimeMessage
## SYNOPSIS
Saves a MIME message or wrapper object to disk.

## SYNTAX
### __AllParameterSets
```powershell
Save-MimeMessage -InputObject <Object> -Path <string> [<CommonParameters>]
```

## DESCRIPTION
Saves a MIME message or wrapper object to disk.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-MimeMessage -InputObject 'Value' -Path 'C:\Path'
```


## PARAMETERS

### -InputObject
Message to save.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: Message
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Path
Destination file path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
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

- `System.Object`

## OUTPUTS

- `None`

## RELATED LINKS

- None
