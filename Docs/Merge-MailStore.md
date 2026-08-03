---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Merge-MailStore
## SYNOPSIS
Merges multiple read-only mail stores into a new Unicode PST.

Delegates folder mapping, bounded retries, semantic deduplication, and PST writing to OfficeIMO.Email. Source PST, OST, OLM, EMLX, Mbox, and mailbox-directory data is never modified.

## SYNTAX
### __AllParameterSets
```powershell
Merge-MailStore [-InputPath] <string[]> [-OutputPath] <string> [-StoreReaderOptions <EmailStoreReaderOptions[]>] [-Force] [-DisplayName <string>] [-FolderMode <EmailStoreMergeFolderMode>] [-DisableDeduplication] [-StopOnSourceError] [-StopOnItemError] [-ExcludeAssociatedItems] [-ExcludeOrphanedItems] [-IncludeSearchFolders] [-MaxItems <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Merges multiple read-only mail stores into a new Unicode PST.

Delegates folder mapping, bounded retries, semantic deduplication, and PST writing to OfficeIMO.Email. Source PST, OST, OLM, EMLX, Mbox, and mailbox-directory data is never modified.

## EXAMPLES

### EXAMPLE 1
```powershell
Merge-MailStore -InputPath @('C:\Path')
```


## PARAMETERS

### -DisableDeduplication
Writes semantically duplicate items instead of deduplicating them.

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

### -FolderMode
Controls whether source roots stay separate, equal paths merge, or all items are flattened.

```yaml
Type: EmailStoreMergeFolderMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: SeparateSourceRoots, MergeByFolderPath, Flatten

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

### -IncludeSearchFolders
Copies search-folder results as static folders and items.

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
Source store files or mailbox directories.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: Path
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxItems
Maximum source items inspected across the merge.

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

### -StopOnItemError
Stops after the first item-read failure.

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

### -StopOnSourceError
Stops after the first source-open failure.

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
Optional source reader limits and PST passwords. Supply one value for every source, or one value per InputPath.

```yaml
Type: EmailStoreReaderOptions[]
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

- `OfficeIMO.Email.Store.EmailStorePstMergeReport`

## RELATED LINKS

- None
