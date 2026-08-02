---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Import-MailFile
## SYNOPSIS
Imports an EML, MSG, OFT, or TNEF mail file and returns its contents as a message object.

The Import-MailFile cmdlet loads a native mail artifact and returns a MailFileMessage for further processing, inspection, or conversion.

## SYNTAX
### __AllParameterSets
```powershell
Import-MailFile [-InputPath] <string> [-IncludeHeaders] [-ExcludeAttachments] [-ExcludeAttachmentContent] [-VerifySignature] [<CommonParameters>]
```

## DESCRIPTION
Imports an EML, MSG, OFT, or TNEF mail file and returns its contents as a message object.

The Import-MailFile cmdlet loads a native mail artifact and returns a MailFileMessage for further processing, inspection, or conversion.

## EXAMPLES

### EXAMPLE 1
```powershell
Import-MailFile -InputPath 'C:\Path'
```


## PARAMETERS

### -ExcludeAttachmentContent
Retains attachment metadata while omitting decoded attachment bytes.

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

### -ExcludeAttachments
Omits attachments from the compatibility projection.

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

### -IncludeHeaders
Includes the merged source headers in the compatibility projection.

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

### -InputPath
Specifies the path to the .msg or .eml file to
import. Supported inputs are .eml, .msg, .oft, .tnef, and winmail.dat. Accepts aliases FilePath and Path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: FilePath, Path
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VerifySignature
Verifies retained EML, MSG, or TNEF S/MIME signatures and projects the bounded OfficeIMO result.

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

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
