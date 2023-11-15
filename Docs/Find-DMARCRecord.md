---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Find-DMARCRecord

## SYNOPSIS
Queries DNS to provide DMARC information

## SYNTAX

```
Find-DMARCRecord [-DomainName] <Array> [-DnsServer <String>] [-DNSProvider <String>] [-AsHashTable] [-AsObject]
 [<CommonParameters>]
```

## DESCRIPTION
Queries DNS to provide DMARC information

## EXAMPLES

### EXAMPLE 1
```
# Standard way
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
```

### EXAMPLE 2
```
# Https way via Cloudflare
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *
```

### EXAMPLE 3
```
# Https way via Google
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *
```

## PARAMETERS

### -DomainName
Name/DomainName to query for DMARC record

```yaml
Type: Array
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -DnsServer
Allows to choose DNS IP address to ask for DNS query.
By default uses system ones.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DNSProvider
Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AsHashTable
Returns Hashtable instead of PSCustomObject

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

### -AsObject
Returns an object rather than string based represantation for name servers (for easier display purposes)

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
