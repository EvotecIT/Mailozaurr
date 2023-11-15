---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Find-DNSBL

## SYNOPSIS
Searches DNSBL if particular IP is blocked on DNSBL.

## SYNTAX

### Default (Default)
```powershell
Find-DNSBL -IP <String[]> [-BlockListServers <String[]>] [-All] [<CommonParameters>]
```

### DNSServer
```powershell
Find-DNSBL -IP <String[]> [-BlockListServers <String[]>] [-All] [-DNSServer <String>] [<CommonParameters>]
```

### DNSProvider
```powershell
Find-DNSBL -IP <String[]> [-BlockListServers <String[]>] [-All] [-DNSProvider <String>] [<CommonParameters>]
```

## DESCRIPTION
Searches DNSBL if particular IP is blocked on DNSBL.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-DNSBL -IP '89.74.48.96' | Format-Table
```

### EXAMPLE 2
```powershell
Find-DNSBL -IP '89.74.48.96', '89.74.48.97', '89.74.48.98' | Format-Table
```

### EXAMPLE 3
```powershell
Find-DNSBL -IP '89.74.48.96' -DNSServer 1.1.1.1 | Format-Table
```

### EXAMPLE 4
```powershell
Find-DNSBL -IP '89.74.48.96' -DNSProvider Cloudflare | Format-Table
```

## PARAMETERS

### -IP
IP to check if it exists on DNSBL

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BlockListServers
Provide your own blocklist of servers

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: $Script:BlockList
Accept pipeline input: False
Accept wildcard characters: False
```

### -All
Return All entries.
By default it returns only those on DNSBL.

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

### -DNSServer
Allows to choose DNS IP address to ask for DNS query.
By default uses system ones.

```yaml
Type: String
Parameter Sets: DNSServer
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
Parameter Sets: DNSProvider
Aliases:

Required: False
Position: Named
Default value: None
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
