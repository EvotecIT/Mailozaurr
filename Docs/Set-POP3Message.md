---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Set-POP3Message
## SYNOPSIS
Updates local flags on a POP3 message.

## SYNTAX
### __AllParameterSets
```powershell
Set-POP3Message [[-Client] <PopConnectionInfo>] [-Index] <int> [-Read] [-Unread] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Updates local flags on a POP3 message.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-POP3Message -Client 'Value'
```


## PARAMETERS

### -Client
Active POP3 connection.

```yaml
Type: PopConnectionInfo
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Index
Message index.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Read
Marks the message as read.

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

### -Unread
Marks the message as unread.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `Mailozaurr.PowerShell.PopConnectionInfo`: Represents the result of a successful POP3 connection, including the client and connection details.

## OUTPUTS

- `None`

## RELATED LINKS

- None
