---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Test-MailStore
## SYNOPSIS
Runs bounded validation against an imported mail store.

Validates PST, OST, OLM, EMLX, Mbox, or mailbox-directory data at shallow, summary, or full-item depth. Structural PST/OST page and block verification is opt-in.

## SYNTAX
### __AllParameterSets
```powershell
Test-MailStore [-InputObject] <Object> [-Mode <EmailStoreValidationMode>] [-FolderId <string>] [-IncludeDescendants] [-IncludeAssociatedItems] [-ExcludeOrphanedItems] [-MaxItems <int>] [-VerifyStructuralIntegrity] [<CommonParameters>]
```

## DESCRIPTION
Runs bounded validation against an imported mail store.

Validates PST, OST, OLM, EMLX, Mbox, or mailbox-directory data at shallow, summary, or full-item depth. Structural PST/OST page and block verification is opt-in.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-MailStore -ExcludeOrphanedItems
```


## PARAMETERS

### -ExcludeOrphanedItems
Excludes recoverable items absent from normal folder tables.

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

### -MaxItems
Maximum item references validated.

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

### -Mode
Validation depth.

```yaml
Type: EmailStoreValidationMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Shallow, Summaries, FullItems

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VerifyStructuralIntegrity
Verifies PST/OST structural page and block integrity within bounded limits.

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

- `OfficeIMO.Email.Store.EmailStoreValidationReport`

## RELATED LINKS

- None
