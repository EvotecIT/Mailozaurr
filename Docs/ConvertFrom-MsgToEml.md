---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertFrom-MsgToEml
## SYNOPSIS
Converts MSG files to EML format for interoperability with other clients.

The ConvertFrom-MsgToEml cmdlet converts one or more MSG files to EML format. Provide input MSG file paths and the destination folder. Existing files can be overwritten with the -Force switch.

## SYNTAX
### __AllParameterSets
```powershell
ConvertFrom-MsgToEml [-InputPath] <string[]> [-OutputFolder] <string> [-Force] [<CommonParameters>]
```

## DESCRIPTION
Converts MSG files to EML format for interoperability with other clients.

The ConvertFrom-MsgToEml cmdlet converts one or more MSG files to EML format. Provide input MSG file paths and the destination folder. Existing files can be overwritten with the -Force switch.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-MsgToEml -InputPath @('C:\Path')
```


## PARAMETERS

### -Force
Overwrite existing files.

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

### -InputPath
Paths to the MSG files to convert.

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

### -OutputFolder
Destination folder for the converted EML files.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: OutputPath
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

- `System.String[]`

## OUTPUTS

- `None`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
