---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# ConvertTo-GraphCredential

## SYNOPSIS
{{ Fill in the Synopsis }}

## SYNTAX

### ClearText (Default)
```
ConvertTo-GraphCredential -ClientID <String> -ClientSecret <String> -DirectoryID <String> [<CommonParameters>]
```

### Encrypted
```
ConvertTo-GraphCredential -ClientID <String> -ClientSecretEncrypted <String> -DirectoryID <String>
 [<CommonParameters>]
```

### MsalToken
```
ConvertTo-GraphCredential -MsalToken <String> [<CommonParameters>]
```

### MsalTokenEncrypted
```
ConvertTo-GraphCredential -MsalTokenEncrypted <String> [<CommonParameters>]
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
Parameter Sets: ClearText, Encrypted
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClientSecret
{{ Fill ClientSecret Description }}

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

### -ClientSecretEncrypted
{{ Fill ClientSecretEncrypted Description }}

```yaml
Type: String
Parameter Sets: Encrypted
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DirectoryID
{{ Fill DirectoryID Description }}

```yaml
Type: String
Parameter Sets: ClearText, Encrypted
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MsalToken
{{ Fill MsalToken Description }}

```yaml
Type: String
Parameter Sets: MsalToken
Aliases: Token

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MsalTokenEncrypted
{{ Fill MsalTokenEncrypted Description }}

```yaml
Type: String
Parameter Sets: MsalTokenEncrypted
Aliases: TokenEncrypted

Required: True
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
