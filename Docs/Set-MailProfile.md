---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Set-MailProfile
## SYNOPSIS
Updates non-secret fields of an existing Mailozaurr profile.

## SYNTAX
### __AllParameterSets
```powershell
Set-MailProfile [-ProfileId] <string> [-DisplayName <string>] [-Kind <MailProfileKind>] [-Description <string>] [-DefaultSender <string>] [-DefaultMailbox <string>] [-Settings <IDictionary>] [-RemoveSetting <string[]>] [-ClearSettings] [-IsDefault] [-ProfileDirectory <string>] [-SecretDirectory <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Updates non-secret fields of an existing Mailozaurr profile.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-MailProfile -ClearSettings
```


## PARAMETERS

### -ClearSettings
Clears all non-secret settings before applying Settings.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DefaultMailbox
Updated default mailbox. Pass an empty string to clear it.

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

### -DefaultSender
Updated default sender. Pass an empty string to clear it.

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

### -Description
Updated description. Pass an empty string to clear it.

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

### -DisplayName
Updated user-facing name.

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

### -IsDefault
Makes this the default profile.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Kind
Updated provider kind.

```yaml
Type: MailProfileKind
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Unknown, Imap, Pop3, Graph, Gmail, Smtp, SendGrid, Mailgun, Ses, Jmap

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProfileDirectory
Optional directory containing the profile store.

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

### -ProfileId
Profile identifier.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Id
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -RemoveSetting
Setting names to remove.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecretDirectory
Optional directory containing the protected secret store.

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

### -Settings
Non-secret settings to merge into the profile.

```yaml
Type: IDictionary
Parameter Sets: __AllParameterSets
Aliases: Setting
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

- `System.String`

## OUTPUTS

- `Mailozaurr.OperationResult`

## RELATED LINKS

- None
