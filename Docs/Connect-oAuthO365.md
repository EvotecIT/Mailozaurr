---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Connect-oAuthO365

## SYNOPSIS
{{ Fill in the Synopsis }}

## SYNTAX

```
Connect-oAuthO365 [[-Login] <String>] [-ClientID] <String> [-TenantID] <String> [[-RedirectUri] <Uri>]
 [[-Scopes] <String[]>] [<CommonParameters>]
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

### -ClientID
{{ Fill ClientID Description }}

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Login
{{ Fill Login Description }}

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RedirectUri
{{ Fill RedirectUri Description }}

```yaml
Type: Uri
Parameter Sets: (All)
Aliases:

Required: False
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scopes
{{ Fill Scopes Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:
Accepted values: email, offline_access, https://outlook.office.com/IMAP.AccessAsUser.All, https://outlook.office.com/POP.AccessAsUser.All, https://outlook.office.com/SMTP.Send

Required: False
Position: 4
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TenantID
{{ Fill TenantID Description }}

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
