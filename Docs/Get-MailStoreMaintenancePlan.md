---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailStoreMaintenancePlan
## SYNOPSIS
Plans read-only maintenance for an imported mail store.

Returns OfficeIMO.Email's source-bound validation, recovery evidence, and recommendations. The plan does not modify the store.

## SYNTAX
### __AllParameterSets
```powershell
Get-MailStoreMaintenancePlan [-InputObject] <Object> [-MaxItems <int>] [<CommonParameters>]
```

## DESCRIPTION
Plans read-only maintenance for an imported mail store.

Returns OfficeIMO.Email's source-bound validation, recovery evidence, and recommendations. The plan does not modify the store.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailStoreMaintenancePlan -InputObject 'Value'
```


## PARAMETERS

### -InputObject
Import-MailData result containing a store, or a native session.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaxItems
Maximum item references inspected while planning.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `OfficeIMO.Email.Store.EmailStoreMaintenancePlan`

## RELATED LINKS

- None
