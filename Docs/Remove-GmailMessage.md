---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Remove-GmailMessage
## SYNOPSIS
Deletes a Gmail message.

## SYNTAX
### __AllParameterSets
```powershell
Remove-GmailMessage -GmailAccount <string> -Credential <pscredential> -Id <string> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Deletes a Gmail message.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-GmailMessage -GmailAccount 'Value' -Credential Get-Credential -Id 'Value'
```


## PARAMETERS

### -Credential
OAuth credential used to authenticate.

```yaml
Type: PSCredential
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GmailAccount
Gmail account containing the message.

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

### -Id
Identifier of the Gmail message to remove.

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

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- None
