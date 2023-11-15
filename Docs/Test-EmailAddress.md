---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Test-EmailAddress

## SYNOPSIS
Checks if email address matches conditions to be valid email address.

## SYNTAX

```powershell
Test-EmailAddress [-EmailAddress] <String[]> [<CommonParameters>]
```

## DESCRIPTION
Checks if email address matches conditions to be valid email address.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-EmailAddress -EmailAddress 'przemyslaw.klys@test'
```

### EXAMPLE 2
```powershell
Test-EmailAddress -EmailAddress 'przemyslaw.klys@test.pl'
```

### EXAMPLE 3
```powershell
Test-EmailAddress -EmailAddress 'przemyslaw.klys@test','przemyslaw.klys@test.pl'
```

## PARAMETERS

### -EmailAddress
EmailAddress to check

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES
General notes

## RELATED LINKS
