---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-GmailMessage
## SYNOPSIS
Retrieves messages using the Gmail API.

## SYNTAX
### List
```powershell
Get-GmailMessage -GmailAccount <string> -Credential <pscredential> [-Query <string>] [-MaxResults <Int32>] [<CommonParameters>]
```

### Id
```powershell
Get-GmailMessage -GmailAccount <string> -Credential <pscredential> -Id <string> [<CommonParameters>]
```

## DESCRIPTION
Retrieves messages using the Gmail API.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-GmailMessage -GmailAccount 'Value' -Credential Get-Credential -Id 'Value'
```


### EXAMPLE 2
```powershell
Get-GmailMessage -GmailAccount 'Value' -Credential Get-Credential
```


## PARAMETERS

### -Credential
OAuth credential used to authenticate to Gmail.

```yaml
Type: PSCredential
Parameter Sets: List, Id
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GmailAccount
Gmail account address to operate on.

```yaml
Type: String
Parameter Sets: List, Id
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Id
Identifier of a specific Gmail message.

```yaml
Type: String
Parameter Sets: Id
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxResults
Maximum number of messages to return when listing.

```yaml
Type: Int32
Parameter Sets: List
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Query
Search query used when listing messages.

```yaml
Type: String
Parameter Sets: List
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

- `Mailozaurr.GmailMessage`

## RELATED LINKS

- None
