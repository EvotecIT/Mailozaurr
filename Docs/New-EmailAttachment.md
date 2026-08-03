---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-EmailAttachment
## SYNOPSIS
Creates an attachment descriptor for Mailozaurr send cmdlets.

## SYNTAX
### Path (Default)
```powershell
New-EmailAttachment [-Path] <string> [-ContentType <string>] [-ContentId <string>] [<CommonParameters>]
```

### Bytes
```powershell
New-EmailAttachment -Bytes <byte[]> [-FileName <string>] [-ContentType <string>] [-ContentId <string>] [<CommonParameters>]
```

### Text
```powershell
New-EmailAttachment -Text <string> [-FileName <string>] [-ContentType <string>] [-ContentId <string>] [-Encoding <Encoding>] [<CommonParameters>]
```

### Stream
```powershell
New-EmailAttachment -Stream <Stream> [-FileName <string>] [-ContentType <string>] [-ContentId <string>] [<CommonParameters>]
```

## DESCRIPTION
Creates an attachment descriptor for Mailozaurr send cmdlets.

## EXAMPLES

### EXAMPLE 1
```powershell
New-EmailAttachment -Path 'C:\Path'
```


### EXAMPLE 2
```powershell
New-EmailAttachment -Bytes @('Value')
```


### EXAMPLE 3
```powershell
New-EmailAttachment -Stream 'Value'
```


## PARAMETERS

### -Bytes
Attachment content as bytes.

```yaml
Type: Byte[]
Parameter Sets: Bytes
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContentId
Content id used when the attachment is sent inline.

```yaml
Type: String
Parameter Sets: Path, Bytes, Text, Stream
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContentType
MIME content type for the attachment.

```yaml
Type: String
Parameter Sets: Path, Bytes, Text, Stream
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Encoding
Text encoding used with Text content. Defaults to UTF-8.

```yaml
Type: Encoding
Parameter Sets: Text
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FileName
File name to use for non-path attachment content.

```yaml
Type: String
Parameter Sets: Bytes, Text, Stream
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
File path used as attachment content.

```yaml
Type: String
Parameter Sets: Path
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Stream
Attachment content as a readable stream.

```yaml
Type: Stream
Parameter Sets: Stream
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Text
Attachment content as text.

```yaml
Type: String
Parameter Sets: Text
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

- `None`

## OUTPUTS

- `Mailozaurr.Definitions.AttachmentDescriptor`

## RELATED LINKS

- None
