---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Test-SmtpConnection
## SYNOPSIS
Tests SMTP connectivity and reports server capabilities.

The Test-SmtpConnection cmdlet connects to an
SMTP server and returns information about supported features. It also checks
if the connection remains open after a NOOP command which indicates support
for persistent connections. It can also perform an envelope-only recipient
probe or send an explicit validation message for authorized mail-flow testing.

## SYNTAX
### Connection (Default)
```powershell
Test-SmtpConnection [-Server <string>] [-Domain <string>] [-ExchangeOnlineDirect] [-Port <int>] [-UseSsl] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RecipientProbe
```powershell
Test-SmtpConnection -Recipient <string> [-Server <string>] [-Domain <string>] [-ExchangeOnlineDirect] [-Port <int>] [-UseSsl] [-Sender <string>] [-HeloHost <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ValidationMessage
```powershell
Test-SmtpConnection -Recipient <string> -SendValidationMessage -IUnderstandThisSendsEmail [-Server <string>] [-Domain <string>] [-ExchangeOnlineDirect] [-Port <int>] [-UseSsl] [-Sender <string>] [-HeloHost <string>] [-TestId <string>] [-ValidationSubject <string>] [-ValidationBody <string>] [-HighPriority] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Tests SMTP connectivity and reports server capabilities.

The Test-SmtpConnection cmdlet connects to an
SMTP server and returns information about supported features. It also checks
if the connection remains open after a NOOP command which indicates support
for persistent connections. It can also perform an envelope-only recipient
probe or send an explicit validation message for authorized mail-flow testing.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-SmtpConnection -Domain 'Value'
```


### EXAMPLE 2
```powershell
Test-SmtpConnection -Recipient 'Value'
```


### EXAMPLE 3
```powershell
Test-SmtpConnection -Recipient 'Value' -SendValidationMessage -IUnderstandThisSendsEmail
```


## PARAMETERS

### -Domain
External email domain used to infer the direct Exchange Online Protection target.

```yaml
Type: String
Parameter Sets: Connection, RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExchangeOnlineDirect
Infers the direct Exchange Online Protection target from the domain instead of requiring -Server.

```yaml
Type: SwitchParameter
Parameter Sets: Connection, RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HeloHost
EHLO/HELO hostname used during recipient probing.

```yaml
Type: String
Parameter Sets: RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HighPriority
Marks the validation message as high priority.

```yaml
Type: SwitchParameter
Parameter Sets: ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IUnderstandThisSendsEmail
Explicit acknowledgement that the validation mode sends an email message.

```yaml
Type: SwitchParameter
Parameter Sets: ValidationMessage
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
TCP port used for the SMTP connection.

```yaml
Type: Int32
Parameter Sets: Connection, RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recipient
Optional recipient address to validate with RCPT TO without sending message DATA.

```yaml
Type: String
Parameter Sets: RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Sender
Envelope sender address used with MAIL FROM during recipient probing.

```yaml
Type: String
Parameter Sets: RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SendValidationMessage
Sends a neutral validation message after the connection and recipient probe.

```yaml
Type: SwitchParameter
Parameter Sets: ValidationMessage
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
SMTP server hostname to test.

```yaml
Type: String
Parameter Sets: Connection, RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TestId
Optional validation test identifier used in the subject, body, and custom header.

```yaml
Type: String
Parameter Sets: ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseSsl
Indicates whether to test SSL connectivity.

```yaml
Type: SwitchParameter
Parameter Sets: Connection, RecipientProbe, ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ValidationBody
Optional plain-text body used for the validation message.

```yaml
Type: String
Parameter Sets: ValidationMessage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ValidationSubject
Optional subject used for the validation message.

```yaml
Type: String
Parameter Sets: ValidationMessage
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

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- None
