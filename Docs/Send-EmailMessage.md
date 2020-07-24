---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Send-EmailMessage

## SYNOPSIS
Short description

## SYNTAX

```
Send-EmailMessage [[-Server] <String>] [[-Port] <Int32>] [[-From] <Object>] [[-ReplyTo] <String>]
 [[-Cc] <String[]>] [[-Bcc] <String[]>] [[-To] <String[]>] [[-Subject] <String>] [[-Priority] <String>]
 [[-Encoding] <String>] [[-DeliveryNotificationOption] <String[]>]
 [[-DeliveryStatusNotificationType] <DeliveryStatusNotificationType>] [[-Credential] <PSCredential>]
 [[-Username] <String>] [[-Password] <String>] [[-SecureSocketOptions] <SecureSocketOptions>] [-UseSsl]
 [[-HTML] <String[]>] [[-Text] <String[]>] [[-Attachment] <String[]>] [[-Timeout] <Int32>] [-ShowErrors]
 [-Suppress] [[-Email] <IDictionary>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Long description

## EXAMPLES

### EXAMPLE 1
```
An example
```

## PARAMETERS

### -Attachment
{{ Fill Attachment Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: Attachments

Required: False
Position: 18
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Bcc
Parameter description

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 5
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Cc
{{ Fill Cc Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 4
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
{{ Fill Credential Description }}

```yaml
Type: PSCredential
Parameter Sets: (All)
Aliases:

Required: False
Position: 12
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryNotificationOption
{{ Fill DeliveryNotificationOption Description }}

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:
Accepted values: None, OnSuccess, OnFailure, Delay, Never

Required: False
Position: 10
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryStatusNotificationType
{{ Fill DeliveryStatusNotificationType Description }}

```yaml
Type: DeliveryStatusNotificationType
Parameter Sets: (All)
Aliases:
Accepted values: Unspecified, Full, HeadersOnly

Required: False
Position: 11
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Email
Parameter description

```yaml
Type: IDictionary
Parameter Sets: (All)
Aliases: EmailParameters

Required: False
Position: 20
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Encoding
Parameter description

```yaml
Type: String
Parameter Sets: (All)
Aliases:
Accepted values: ASCII, BigEndianUnicode, Default, Unicode, UTF32, UTF7, UTF8

Required: False
Position: 9
Default value: Default
Accept pipeline input: False
Accept wildcard characters: False
```

### -From
Parameter description

```yaml
Type: Object
Parameter Sets: (All)
Aliases:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HTML
Parameter description

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: Body

Required: False
Position: 16
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
{{ Fill Password Description }}

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 14
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
Parameter description

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: 1
Default value: 587
Accept pipeline input: False
Accept wildcard characters: False
```

### -Priority
Parameter description

```yaml
Type: String
Parameter Sets: (All)
Aliases:
Accepted values: Low, Normal, High

Required: False
Position: 8
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplyTo
Parameter description

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecureSocketOptions
Parameter description

```yaml
Type: SecureSocketOptions
Parameter Sets: (All)
Aliases:
Accepted values: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable

Required: False
Position: 15
Default value: Auto
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
Parameter description

```yaml
Type: String
Parameter Sets: (All)
Aliases: SmtpServer

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ShowErrors
Parameter description

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

### -Subject
Parameter description

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 7
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Suppress
Parameter description

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

### -Text
Parameter description

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 17
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
{{ Fill Timeout Description }}

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: 19
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -To
Parameter description

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 6
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseSsl
Parameter description

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

### -Username
{{ Fill Username Description }}

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 13
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs. The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

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
