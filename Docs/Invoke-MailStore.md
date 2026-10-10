---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Invoke-MailStore
## SYNOPSIS
Runs a script block with an automatically closed local mail-store scope.

Passes one store scope to the script block. Repeated Get-MailMessage calls reuse its reader. The store closes on completion, cancellation or a terminating error. Returned message views remain usable while their source file is unchanged.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-MailStore [-Path] <string> [-ScriptBlock] <scriptblock> [<CommonParameters>]
```

## DESCRIPTION
Runs a script block with an automatically closed local mail-store scope.

Passes one store scope to the script block. Repeated Get-MailMessage calls reuse its reader. The store closes on completion, cancellation or a terminating error. Returned message views remain usable while their source file is unchanged.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-MailStore './Archive.pst' { param($store) $store | Get-MailMessage -Folder Inbox -First 10 }
```


## PARAMETERS

### -Path
Local archive file.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScriptBlock
Script block accepting the store as its first argument.

```yaml
Type: ScriptBlock
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `System.Object`

## RELATED LINKS

- None
