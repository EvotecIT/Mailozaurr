---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertFrom-EmlToMsg
## SYNOPSIS
Converts EML files to MSG format for compatibility with Microsoft Outlook and other clients.

The ConvertFrom-EmlToMsg cmdlet converts one or more EML files to MSG format. Specify the input EML file paths and the output folder. The cmdlet processes each EML file and saves the converted MSG file in the specified output folder. Supports overwriting existing files with the -Force parameter.

## SYNTAX
### __AllParameterSets
```powershell
ConvertFrom-EmlToMsg [-InputPath] <string[]> [-OutputFolder] <string> [-Force] [<CommonParameters>]
```

## DESCRIPTION
Converts EML files to MSG format for compatibility with Microsoft Outlook and other clients.

The ConvertFrom-EmlToMsg cmdlet converts one or more EML files to MSG format. Specify the input EML file paths and the output folder. The cmdlet processes each EML file and saves the converted MSG file in the specified output folder. Supports overwriting existing files with the -Force parameter.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-EmlToMsg -InputPath @('C:\Path')
```


## PARAMETERS

### -Force
If set, the cmdlet will overwrite existing MSG files without prompting.

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
Specifies the paths to the EML files to convert. Accepts an array of strings. This parameter is mandatory.

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
Specifies the folder where the converted MSG files will be saved. This parameter is mandatory.

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
