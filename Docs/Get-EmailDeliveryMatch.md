---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-EmailDeliveryMatch
## SYNOPSIS
Searches for non-delivery reports and matches them to sent messages.

The Get-EmailDeliveryMatch cmdlet searches for non-delivery reports and uses a SendLogResolver to correlate them with sent messages.

## SYNTAX
### __AllParameterSets
```powershell
Get-EmailDeliveryMatch -Protocol <EmailProtocol> -Resolver <SendLogResolver> [-Recipient <string>] [-MessageId <string>] [-Since <datetime>] [-Before <datetime>] [-Count <int>] [-Folder <string>] [-UserPrincipalName <string>] [<CommonParameters>]
```

## DESCRIPTION
Searches for non-delivery reports and matches them to sent messages.

The Get-EmailDeliveryMatch cmdlet searches for non-delivery reports and uses a SendLogResolver to correlate them with sent messages.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-EmailDeliveryMatch -Protocol 'Value' -Resolver 'Value'
```


## PARAMETERS

### -Before
Only reports before this time are returned.

```yaml
Type: Nullable`1
Parameter Sets: __AllParameterSets
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
Parameter Sets: __AllParameterSets
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
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageId
Only reports matching this message ID are returned.

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

### -Protocol
Mail protocol to use.

```yaml
Type: EmailProtocol
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Imap, Pop3, Graph, GmailApi

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recipient
Only reports for recipients containing this value are returned.

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

### -Resolver
Resolver used to correlate NDRs with sent messages.

```yaml
Type: SendLogResolver
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Since
Only reports since this time are returned.

```yaml
Type: Nullable`1
Parameter Sets: __AllParameterSets
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

- `None`

## OUTPUTS

- `Mailozaurr.NonDeliveryReports.NonDeliveryReportResult`

## RELATED LINKS

- None
