---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Save-GraphMessage
## SYNOPSIS
Saves Microsoft Graph email messages to disk in a specified format.

The Save-GraphMessage cmdlet saves one or more EmailGraphMessage objects to disk at the specified path. Use this to archive, export, or process messages retrieved from Microsoft Graph.

## SYNTAX
### __AllParameterSets
```powershell
Save-GraphMessage -Message <psobject[]> -Path <string> [<CommonParameters>]
```

## DESCRIPTION
Saves Microsoft Graph email messages to disk in a specified format.

The Save-GraphMessage cmdlet saves one or more EmailGraphMessage objects to disk at the specified path. Use this to archive, export, or process messages retrieved from Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-GraphMessage -Message @('Value') -Path 'C:\Path'
```


## PARAMETERS

### -Message
Specifies the EmailGraphMessage objects to save. Accepts pipeline input.

```yaml
Type: PSObject[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Path
Specifies the path where the messages will be saved.

```yaml
Type: String
Parameter Sets: __AllParameterSets
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

- `System.Management.Automation.PSObject[]`

## OUTPUTS

- `None`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
