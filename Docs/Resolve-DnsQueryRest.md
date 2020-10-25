---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Resolve-DnsQueryRest

## SYNOPSIS
Provides basic DNS Query via HTTPS

## SYNTAX

```
Resolve-DnsQueryRest [-Name] <String> [-Type] <String> [-DNSProvider <String>] [-All] [<CommonParameters>]
```

## DESCRIPTION
Provides basic DNS Query via HTTPS - tested only for use cases within Mailozaurr

## EXAMPLES

### EXAMPLE 1
```
Resolve-DnsQueryRest -Name 'evotec.pl' -Type TXT -DNSProvider Cloudflare
```

## PARAMETERS

### -Name
Name/DomainName to query DNS

```yaml
Type: String
Parameter Sets: (All)
Aliases: Query

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Type of a query A, PTR, MX and so on

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DNSProvider
Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google).
Default is Cloudflare

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: Cloudflare
Accept pipeline input: False
Accept wildcard characters: False
```

### -All
Returns full output rather than just custom, translated data

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES
General notes

## RELATED LINKS
