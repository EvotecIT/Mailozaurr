---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Find-MxRecord

## SYNOPSIS
Queries DNS to provide MX information

## SYNTAX

```
Find-MxRecord [-DomainName] <Array> [-DnsServer <String>] [-DNSProvider <String>] [-ResolvePTR] [-AsHashTable]
 [-Separate] [-AsObject] [<CommonParameters>]
```

## DESCRIPTION
Queries DNS to provide MX information

## EXAMPLES

### EXAMPLE 1
```
# Standard way
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
```

### EXAMPLE 2
```
# Https way via Cloudflare
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *
```

### EXAMPLE 3
```
# Https way via Google
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *
```

### EXAMPLE 4
```
# Standard way with ResolvePTR
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -ResolvePTR | Format-Table *
```

### EXAMPLE 5
```
# Https way via Cloudflare with ResolvePTR
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare -ResolvePTR | Format-Table *
```

### EXAMPLE 6
```
# Https way via Google with ResolvePTR
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google -ResolvePTR | Format-Table *
```

## PARAMETERS

### -DomainName
Name/DomainName to query for MX record

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

### -ResolvePTR
Parameter description

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

### -Separate
Returns each MX record separatly

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
