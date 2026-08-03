---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertTo-MailPst
## SYNOPSIS
Converts a supported mail store into a new Unicode PST.

Converts PST, supported PST-compatible OST, OLM, Mbox, EMLX, EML, Maildir, Apple Mail, or EML directory sources through OfficeIMO.Email. The source is never modified and semantic verification is enabled by default.

## SYNTAX
### __AllParameterSets
```powershell
ConvertTo-MailPst [-InputPath] <string> [-OutputPath] <string> [-StoreReaderOptions <EmailStoreReaderOptions>] [-Force] [-FailOnDataLoss] [-StopOnItemError] [-ExcludeAssociatedItems] [-ExcludeOrphanedItems] [-ExcludeSearchFolders] [-MaxItems <int>] [-MaxNestedMessageDepth <int>] [-DisplayName <string>] [-SkipVerification] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Converts a supported mail store into a new Unicode PST.

Converts PST, supported PST-compatible OST, OLM, Mbox, EMLX, EML, Maildir, Apple Mail, or EML directory sources through OfficeIMO.Email. The source is never modified and semantic verification is enabled by default.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertTo-MailPst -InputPath 'C:\Path'
```


## PARAMETERS

### -DisplayName
Optional destination store display name.

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

### -ExcludeAssociatedItems
Omits folder-associated information items.

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

### -ExcludeOrphanedItems
Omits recoverable items absent from normal folder tables.

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

### -ExcludeSearchFolders
Omits search-folder results instead of copying them as static folders.

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

### -FailOnDataLoss
Blocks completion when conversion or verification reports semantic loss.

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

### -Force
Allows an existing destination PST to be atomically replaced.

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
Source PST, OST, OLM, Mbox, EMLX, EML, or mailbox-directory path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Path, FullName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxItems
Maximum source items inspected.

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

### -MaxNestedMessageDepth
Maximum embedded-message nesting depth written.

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

### -OutputPath
Destination Unicode PST path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: DestinationPath
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipVerification
Skips the default staged semantic verification before committing the destination.

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

### -StopOnItemError
Stops after the first unreadable source item.

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

### -StoreReaderOptions
Optional source reader limits and PST password.

```yaml
Type: EmailStoreReaderOptions
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

- `OfficeIMO.Email.Store.EmailStorePstConversionReport`

## RELATED LINKS

- None
