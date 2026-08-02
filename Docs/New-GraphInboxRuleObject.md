---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-GraphInboxRuleObject
## SYNOPSIS
Creates a GraphInboxRule object.

## SYNTAX
### Params
```powershell
New-GraphInboxRuleObject -DisplayName <string> [-Sequence <int>] [-Enabled] [-MoveToFolder <string>] [-CopyToFolder <string>] [-Delete] [-ForwardTo <string[]>] [-StopProcessing] [-SenderContains <string[]>] [-RecipientContains <string[]>] [-SubjectContains <string[]>] [-BodyContains <string[]>] [-Importance <string>] [<CommonParameters>]
```

### Builder
```powershell
New-GraphInboxRuleObject -Builder <GraphInboxRuleBuilder> [<CommonParameters>]
```

## DESCRIPTION
Creates a GraphInboxRule object.

## EXAMPLES

### EXAMPLE 1
```powershell
New-GraphInboxRuleObject -Builder 'Value'
```


### EXAMPLE 2
```powershell
New-GraphInboxRuleObject -DisplayName 'Name'
```


## PARAMETERS

### -BodyContains
Body text patterns to match.

```yaml
Type: String[]
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Builder
Builder object used to create the rule.

```yaml
Type: GraphInboxRuleBuilder
Parameter Sets: Builder
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -CopyToFolder
Folder to copy matching messages to.

```yaml
Type: String
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Delete
Deletes matching messages.

```yaml
Type: SwitchParameter
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisplayName
Display name for the inbox rule.

```yaml
Type: String
Parameter Sets: Params
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Enabled
Enables the rule when set.

```yaml
Type: SwitchParameter
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ForwardTo
Addresses to forward matching messages to.

```yaml
Type: String[]
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Importance
Importance level to match.

```yaml
Type: String
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MoveToFolder
Folder to move matching messages to.

```yaml
Type: String
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RecipientContains
Recipient address patterns to match.

```yaml
Type: String[]
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SenderContains
Sender address patterns to match.

```yaml
Type: String[]
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Sequence
Rule processing sequence number.

```yaml
Type: Int32
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StopProcessing
Stops processing additional rules when this rule matches.

```yaml
Type: SwitchParameter
Parameter Sets: Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubjectContains
Subject text patterns to match.

```yaml
Type: String[]
Parameter Sets: Params
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

- `Mailozaurr.GraphInboxRuleBuilder`

## OUTPUTS

- `Mailozaurr.GraphInboxRule`

## RELATED LINKS

- None
