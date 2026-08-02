---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Export-MailFile
## SYNOPSIS
Exports an imported mail file as EML, MSG, or TNEF.

Writes a MailFileMessage to a file and disposes the input after an attempted export unless -KeepInputOpen is specified. Previewed or rejected operations leave the input open. The destination extension selects EML, MSG, or TNEF, so the same command handles conversion in either direction.

## SYNTAX
### __AllParameterSets
```powershell
Export-MailFile [-OutputPath] <string> -InputObject <MailFileMessage> [-Force] [-PassThru] [-KeepInputOpen] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Exports an imported mail file as EML, MSG, or TNEF.

Writes a MailFileMessage to a file and disposes the input after an attempted export unless -KeepInputOpen is specified. Previewed or rejected operations leave the input open. The destination extension selects EML, MSG, or TNEF, so the same command handles conversion in either direction.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-MailFile -InputObject 'Value'
```


## PARAMETERS

### -Force
Overwrites an existing destination file.

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

### -InputObject
Message returned by Import-MailFile or loaded through the .NET API.

```yaml
Type: MailFileMessage
Parameter Sets: __AllParameterSets
Aliases: Message
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -KeepInputOpen
Keeps the input message open after export so the caller can continue using it.

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

### -OutputPath
Destination file. Use .eml, .msg, .tnef, or winmail.dat.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Path
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Returns a FileInfo for the exported file.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.MailFileMessage`

## OUTPUTS

- `System.IO.FileInfo`

## RELATED LINKS

- None
