---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Export-MailStore
## SYNOPSIS
Exports selected items from a PST, OST, OLM, Mbox, EMLX, or mailbox directory.

Delegates to OfficeIMO.Email for EML, MSG, OFT, TNEF, Mbox, Maildir, or EMLX output. The source store remains read-only and the native preservation report is returned.

## SYNTAX
### __AllParameterSets
```powershell
Export-MailStore [-OutputPath] <string> [-Format] <string> -InputObject <Object> [-FolderId <string>] [-IncludeDescendants] [-IncludeAssociatedItems] [-IncludeOrphanedItems] [-Flatten] [-NoManifest] [-StopOnError] [-Force] [-MaxItems <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Exports selected items from a PST, OST, OLM, Mbox, EMLX, or mailbox directory.

Delegates to OfficeIMO.Email for EML, MSG, OFT, TNEF, Mbox, Maildir, or EMLX output. The source store remains read-only and the native preservation report is returned.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-MailStore -InputObject 'Value'
```


## PARAMETERS

### -Flatten
Flattens folder hierarchy for directory-based output.

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

### -FolderId
Optional stable source folder identifier.

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

### -Force
Allows existing destination artifacts to be replaced.

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

### -Format
Output format: Eml, Msg, Oft, Tnef, Mbox, Maildir, or Emlx.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Eml, Msg, Oft, Tnef, Mbox, Maildir, Emlx

Required: True
Position: 1
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
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxItems
Maximum source items attempted.

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

### -NoManifest
Suppresses preservation-manifest output for directory-based exports.

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
Destination file or directory.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Path, DestinationPath
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StopOnError
Stops after the first item read or write failure.

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

- `OfficeIMO.Email.Store.EmailStoreExportReport`
- `OfficeIMO.Email.Store.EmailStoreMboxExportReport`

## RELATED LINKS

- None
