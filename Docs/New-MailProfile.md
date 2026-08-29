---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-MailProfile
## SYNOPSIS
Creates a provider profile over the reusable Mailozaurr profile service.

## SYNTAX
### __AllParameterSets
```powershell
New-MailProfile [-ProfileId] <string> -DisplayName <string> -Kind <MailProfileKind> [-Description <string>] [-DefaultSender <string>] [-DefaultMailbox <string>] [-Settings <IDictionary>] [-IsDefault] [-Force] [-ProfileDirectory <string>] [-SecretDirectory <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates a provider profile over the reusable Mailozaurr profile service.

## EXAMPLES

### EXAMPLE 1
```powershell
New-MailProfile -DisplayName 'Name' -Kind 'Value'
```


## PARAMETERS

### -DefaultMailbox
Default mailbox or principal.

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
Default sender address.

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
Optional description.

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
User-facing profile name.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Allows replacement of an existing profile with the same id.

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
Provider or protocol kind.

```yaml
Type: MailProfileKind
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Unknown, Imap, Pop3, Graph, Gmail, Smtp, SendGrid, Mailgun, Ses, Jmap

Required: True
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
Stable profile identifier.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Id
Possible values:

Required: True
Position: 0
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
Non-secret provider settings such as server, port, client id, or JMAP session URL.

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

- `None`

## OUTPUTS

- `Mailozaurr.OperationResult`

## RELATED LINKS

- None
