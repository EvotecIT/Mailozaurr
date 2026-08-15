---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-DmarcReport
## SYNOPSIS
Searches for DMARC aggregate reports in a mailbox.

The Get-DmarcReport cmdlet queries IMAP, POP3, Microsoft Graph, or Gmail API to find DMARC aggregate reports and expose zipped XML attachments.

## SYNTAX
### GmailApi
```powershell
Get-DmarcReport -Protocol <EmailProtocol> -GmailAccount <string> -Credential <pscredential> [-Domain <string>] [-Since <DateTime>] [-Before <DateTime>] [-Count <int>] [-Folder <string>] [-UserPrincipalName <string>] [-ParallelDownloadLimit <int>] [-MaxUncompressedSize <long>] [<CommonParameters>]
```

## DESCRIPTION
Searches for DMARC aggregate reports in a mailbox.

The Get-DmarcReport cmdlet queries IMAP, POP3, Microsoft Graph, or Gmail API to find DMARC aggregate reports and expose zipped XML attachments.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-DmarcReport -Protocol 'Value' -GmailAccount 'Value' -Credential Get-Credential
```


## PARAMETERS

### -Before
Only reports before this time are returned.

```yaml
Type: DateTime
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Count
Maximum number of reports to return. Default is unlimited.

```yaml
Type: Int32
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
OAuth credential used for Gmail API authentication.

```yaml
Type: PSCredential
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Domain
Optional domain filter.

```yaml
Type: String
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Folder
IMAP folder to search.

```yaml
Type: String
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GmailAccount
Gmail account address when using Gmail API.

```yaml
Type: String
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxUncompressedSize
Maximum uncompressed attachment size to inspect, in bytes.

```yaml
Type: Int64
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ParallelDownloadLimit
Maximum concurrent MIME downloads; set to 1 to disable parallelism.

```yaml
Type: Int32
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Protocol
Mail protocol to use.

```yaml
Type: EmailProtocol
Parameter Sets: GmailApi
Aliases: None
Possible values: Imap, Pop3, Graph, GmailApi

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Since
Only reports since this time are returned.

```yaml
Type: DateTime
Parameter Sets: GmailApi
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
User principal name when using Microsoft Graph.

```yaml
Type: String
Parameter Sets: GmailApi
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

- `Mailozaurr.DmarcReports.DmarcReport`

## RELATED LINKS

- None
