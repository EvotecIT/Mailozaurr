---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailStoreItem
## SYNOPSIS
Enumerates or reads items from an imported mail store.

Returns lightweight OfficeIMO.Email item references by default. Use Read to project selected message parts while the owning store remains open.

## SYNTAX
### __AllParameterSets
```powershell
Get-MailStoreItem [-InputObject] <Object> [-FolderId <string>] [-IncludeDescendants] [-IncludeAssociatedItems] [-IncludeOrphanedItems] [-MaxItems <int>] [-Read] [-Parts <EmailStoreItemReadParts>] [-MaxDecodedPropertyBytes <Int64>] [-PreferStreamingAttachmentContent] [<CommonParameters>]
```

## DESCRIPTION
Enumerates or reads items from an imported mail store.

Returns lightweight OfficeIMO.Email item references by default. Use Read to project selected message parts while the owning store remains open.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailStoreItem -FolderId 'Value'
```


## PARAMETERS

### -FolderId
Optional stable folder identifier.

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

### -IncludeAssociatedItems
Includes folder-associated information items.

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

### -IncludeDescendants
Includes descendants of FolderId.

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

### -IncludeOrphanedItems
Includes recoverable items absent from normal folder tables.

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
An Import-MailData result containing a store, or a native EmailStoreSession.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: Store
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaxDecodedPropertyBytes
Optional per-item bound for decoded MAPI property bytes.

```yaml
Type: Int64
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxItems
Maximum item references returned or read.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Parts
Parts requested when Read is specified.

```yaml
Type: EmailStoreItemReadParts
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: None, Metadata, Bodies, Recipients, AttachmentMetadata, AttachmentContent, EmbeddedItems, ExtendedMapiProperties, All

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PreferStreamingAttachmentContent
Prefers reopenable attachment streams over retained byte arrays when supported.

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

### -Read
Reads selected item content instead of returning lightweight references.

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

- `System.Object`

## OUTPUTS

- `OfficeIMO.Email.Store.EmailStoreItemReference`
- `OfficeIMO.Email.Store.EmailStoreItem`

## RELATED LINKS

- None
