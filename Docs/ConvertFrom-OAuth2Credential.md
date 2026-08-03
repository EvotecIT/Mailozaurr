---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# ConvertFrom-OAuth2Credential
## SYNOPSIS
Extracts username and token from an OAuth2 PSCredential.

The ConvertFrom-OAuth2Credential cmdlet converts a PSCredential containing an OAuth2 access token into a PSCustomObject with UserName and Token properties.

## SYNTAX
### __AllParameterSets
```powershell
ConvertFrom-OAuth2Credential -Credential <pscredential> [<CommonParameters>]
```

## DESCRIPTION
Extracts username and token from an OAuth2 PSCredential.

The ConvertFrom-OAuth2Credential cmdlet converts a PSCredential containing an OAuth2 access token into a PSCustomObject with UserName and Token properties.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-OAuth2Credential -Credential Get-Credential
```


## PARAMETERS

### -Credential
Specifies the PSCredential containing the OAuth2 token.

```yaml
Type: PSCredential
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Management.Automation.PSCredential`

## OUTPUTS

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
