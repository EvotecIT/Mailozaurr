---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Export-MailFile
## SYNOPSIS
Exports messages as EML, MSG, OFT, TNEF, or local HTML copies.

Accepts Get-MailMessage views, native documents, or Import-MailFile messages. OutputPath exports one message with format inferred from its extension. OutputDirectory exports message views with portable source-specific names. HTML saves embedded images locally; IncludeAttachments also saves regular files and adds links. Remote resources are blocked. Legacy Import-MailFile input is disposed after an attempted export unless KeepInputOpen is used; Get-MailMessage views remain usable.

## SYNTAX
### File (Default)
```powershell
Export-MailFile [-OutputPath] <string> -InputObject <Object> [-IncludeAttachments] [-AllowLoss] [-Force] [-PassThru] [-KeepInputOpen] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Directory
```powershell
Export-MailFile -InputObject <Object> -OutputDirectory <string> -Format <string> [-IncludeAttachments] [-AllowLoss] [-Force] [-PassThru] [-KeepInputOpen] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Exports messages as EML, MSG, OFT, TNEF, or local HTML copies.

Accepts Get-MailMessage views, native documents, or Import-MailFile messages. OutputPath exports one message with format inferred from its extension. OutputDirectory exports message views with portable source-specific names. HTML saves embedded images locally; IncludeAttachments also saves regular files and adds links. Remote resources are blocked. Legacy Import-MailFile input is disposed after an attempted export unless KeepInputOpen is used; Get-MailMessage views remain usable.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-MailFile -OutputPath 'C:\Path' -InputObject 'Value'
```


### EXAMPLE 2
```powershell
Export-MailFile -InputObject 'Value' -OutputDirectory 'Value' -Format 'Value'
```


## PARAMETERS

### -AllowLoss
Allows native-format conversion to omit unrepresentable source metadata, reporting losses as warnings.

```yaml
Type: SwitchParameter
Parameter Sets: File, Directory
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Atomically replaces an existing destination file.

```yaml
Type: SwitchParameter
Parameter Sets: File, Directory
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format
Batch output format.

```yaml
Type: String
Parameter Sets: Directory
Aliases: None
Possible values: Eml, Msg, Html, Oft, Tnef

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeAttachments
Include regular attachments and links in HTML exports, in addition to embedded images.

```yaml
Type: SwitchParameter
Parameter Sets: File, Directory
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Get-MailMessage view, native email document, or Import-MailFile message.

```yaml
Type: Object
Parameter Sets: File, Directory
Aliases: Message
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -KeepInputOpen
Keeps legacy Import-MailFile input open after an attempted export.

```yaml
Type: SwitchParameter
Parameter Sets: File, Directory
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputDirectory
Destination directory for Get-MailMessage views, using portable message names.

```yaml
Type: String
Parameter Sets: Directory
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Destination file; extension selects the output format.

```yaml
Type: String
Parameter Sets: File
Aliases: Path
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Returns a FileInfo for each committed message copy.

```yaml
Type: SwitchParameter
Parameter Sets: File, Directory
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

- `System.Object`

## OUTPUTS

- `System.IO.FileInfo`

## RELATED LINKS

- None
