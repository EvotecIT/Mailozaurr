---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailStoreConversation
## SYNOPSIS
Builds a bounded conversation graph from an imported mail store.

Returns OfficeIMO.Email's native graph, including edge confidence and diagnostics.

## SYNTAX
### __AllParameterSets
```powershell
Get-MailStoreConversation [-InputObject] <Object> [-Options <EmailConversationGraphOptions>] [<CommonParameters>]
```

## DESCRIPTION
Builds a bounded conversation graph from an imported mail store.

Returns OfficeIMO.Email's native graph, including edge confidence and diagnostics.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailStoreConversation -InputObject 'Value'
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

### -Options
Optional OfficeIMO conversation scope, bounds, and heuristic policy.

```yaml
Type: EmailConversationGraphOptions
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

- `OfficeIMO.Email.Store.EmailConversationGraph`

## RELATED LINKS

- None
