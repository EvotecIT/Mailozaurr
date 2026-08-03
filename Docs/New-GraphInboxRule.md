---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-GraphInboxRule
## SYNOPSIS
Creates a new inbox rule via Microsoft Graph.

## SYNTAX
### Graph
```powershell
New-GraphInboxRule -UserPrincipalName <string> [-Rule <hashtable>] [-RuleObject <GraphInboxRule>] [-RuleBuilder <GraphInboxRuleBuilder>] [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Params
```powershell
New-GraphInboxRule -UserPrincipalName <string> -DisplayName <string> [-Sequence <int>] [-Enabled] [-MoveToFolder <string>] [-CopyToFolder <string>] [-Delete] [-ForwardTo <string[]>] [-StopProcessing] [-SenderContains <string[]>] [-RecipientContains <string[]>] [-SubjectContains <string[]>] [-BodyContains <string[]>] [-Importance <string>] [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
New-GraphInboxRule -UserPrincipalName <string> -MgGraphRequest [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates a new inbox rule via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
New-GraphInboxRule -UserPrincipalName 'Name'
```


### EXAMPLE 2
```powershell
New-GraphInboxRule -UserPrincipalName 'Name' -MgGraphRequest
```


### EXAMPLE 3
```powershell
New-GraphInboxRule -UserPrincipalName 'Name' -DisplayName 'Name'
```


## PARAMETERS

### -BodyContains
Strings that must appear in the body.

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

### -Connection
Connection information for Microsoft Graph.

```yaml
Type: GraphConnectionInfo
Parameter Sets: Graph, Params
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -CopyToFolder
Destination folder to copy messages to.

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
Deletes messages matching the rule.

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
Display name for the new rule.

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
Determines if the rule is enabled.

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
Message importance level to match.

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

### -MgGraphRequest
Use Invoke-MgGraphRequest for sending requests.

```yaml
Type: SwitchParameter
Parameter Sets: MgGraphRequest
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MoveToFolder
Destination folder to move messages to.

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
Recipients that trigger the rule.

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

### -RetryCount
Number of retry attempts on failure.

```yaml
Type: Int32
Parameter Sets: Graph, Params, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryDelayMilliseconds
Delay between retries in milliseconds.

```yaml
Type: Int32
Parameter Sets: Graph, Params, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Rule
Hashtable definition of the rule.

```yaml
Type: Hashtable
Parameter Sets: Graph
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleBuilder
Builder used to create a rule object.

```yaml
Type: GraphInboxRuleBuilder
Parameter Sets: Graph
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RuleObject
Rule object describing the inbox rule.

```yaml
Type: GraphInboxRule
Parameter Sets: Graph
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SenderContains
Sender addresses that trigger the rule.

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
Order in which the rule is processed.

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
Strings that must appear in the subject.

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

### -TimeoutSeconds
Request timeout in seconds.

```yaml
Type: Int32
Parameter Sets: Graph, Params, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
User principal name owning the mailbox.

```yaml
Type: String
Parameter Sets: Graph, Params, MgGraphRequest
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.GraphConnectionInfo`: Represents an authenticated Microsoft Graph connection.

## OUTPUTS

- `Mailozaurr.GraphInboxRule`

## RELATED LINKS

- None
