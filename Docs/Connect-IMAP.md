---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Connect-IMAP

## SYNOPSIS
{{ Fill in the Synopsis }}

## SYNTAX

### Credential (Default)
```
Connect-IMAP [-Server <String>] [-Port <Int32>] [-Credential <PSCredential>] [-Options <SecureSocketOptions>]
 [-TimeOut <Int32>] [<CommonParameters>]
```

### ClearText
```
Connect-IMAP [-Server <String>] [-Port <Int32>] -UserName <String> -Password <String>
 [-Options <SecureSocketOptions>] [-TimeOut <Int32>] [<CommonParameters>]
```

### oAuth2
```
Connect-IMAP [-Server <String>] [-Port <Int32>] [-Credential <PSCredential>] [-Options <SecureSocketOptions>]
 [-TimeOut <Int32>] [-oAuth2] [<CommonParameters>]
```

## DESCRIPTION
{{ Fill in the Description }}

## EXAMPLES

### Example 1
```powershell
PS C:\> {{ Add example code here }}
```

{{ Add example description here }}

## PARAMETERS

### -Credential
{{ Fill Credential Description }}

```yaml
Type: PSCredential
Parameter Sets: Credential, oAuth2
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Options
{{ Fill Options Description }}

```yaml
Type: SecureSocketOptions
Parameter Sets: (All)
Aliases:
Accepted values: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
{{ Fill Password Description }}

```yaml
Type: String
Parameter Sets: ClearText
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
{{ Fill Port Description }}

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
{{ Fill Server Description }}

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

### -TimeOut
{{ Fill TimeOut Description }}

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserName
{{ Fill UserName Description }}

```yaml
Type: String
Parameter Sets: ClearText
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -oAuth2
{{ Fill oAuth2 Description }}

```yaml
Type: SwitchParameter
Parameter Sets: oAuth2
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

### None

## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
