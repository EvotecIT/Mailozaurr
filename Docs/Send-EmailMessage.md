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

### Compatibility (Default)
```
Send-EmailMessage [-Server <String>] [-Port <Int32>] [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>]
 [-Bcc <String[]>] [-To <String[]>] [-Subject <String>] [-Priority <String>] [-Encoding <String>]
 [-DeliveryNotificationOption <String[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>]
 [-Credential <PSCredential>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-HTML <String[]>]
 [-Text <String[]>] [-Attachment <String[]>] [-Timeout <Int32>] [-ShowErrors] [-Suppress] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### oAuth
```
Send-EmailMessage [-Server <String>] [-Port <Int32>] [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>]
 [-Bcc <String[]>] [-To <String[]>] [-Subject <String>] [-Priority <String>] [-Encoding <String>]
 [-DeliveryNotificationOption <String[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>]
 [-Credential <PSCredential>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-HTML <String[]>]
 [-Text <String[]>] [-Attachment <String[]>] [-Timeout <Int32>] [-oAuth2] [-ShowErrors] [-Suppress] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

### ClearText
```
Send-EmailMessage [-Server <String>] [-Port <Int32>] [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>]
 [-Bcc <String[]>] [-To <String[]>] [-Subject <String>] [-Priority <String>] [-Encoding <String>]
 [-DeliveryNotificationOption <String[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>]
 [-Username <String>] [-Password <String>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl]
 [-HTML <String[]>] [-Text <String[]>] [-Attachment <String[]>] [-Timeout <Int32>] [-ShowErrors] [-Suppress]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Graph
```
Send-EmailMessage [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>] [-Bcc <String[]>] [-To <String[]>]
 [-Subject <String>] [-Priority <String>] [-Credential <PSCredential>] [-HTML <String[]>] [-Text <String[]>]
 [-Attachment <String[]>] [-Graph] [-ShowErrors] [-Suppress] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Grouped
```
Send-EmailMessage [-Email <IDictionary>] [-ShowErrors] [-Suppress] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Long description

## EXAMPLES

### EXAMPLE 1
```
An example
```

## PARAMETERS

### -Server
Parameter description

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText
Aliases: SmtpServer

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
Parameter description

```yaml
Type: Int32
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: 587
Accept pipeline input: False
Accept wildcard characters: False
```

### -From
Parameter description

```yaml
Type: Object
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplyTo
Parameter description

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Cc
{{ Fill Cc Description }}

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Bcc
Parameter description

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -To
Parameter description

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Subject
Parameter description

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Priority
Parameter description

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases: Importance

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Encoding
Parameter description

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: Default
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryNotificationOption
{{ Fill DeliveryNotificationOption Description }}

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryStatusNotificationType
{{ Fill DeliveryStatusNotificationType Description }}

```yaml
Type: DeliveryStatusNotificationType
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:
Accepted values: Unspecified, Full, HeadersOnly

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
Parameter Sets: Compatibility, oAuth, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Username
{{ Fill Username Description }}

```yaml
Type: String
Parameter Sets: ClearText
Aliases:

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

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecureSocketOptions
Parameter description

```yaml
Type: SecureSocketOptions
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:
Accepted values: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable

Required: False
Position: Named
Default value: Auto
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseSsl
Parameter description

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -HTML
Parameter description

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases: Body

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Text
Parameter description

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Attachment
{{ Fill Attachment Description }}

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, Graph
Aliases: Attachments

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
{{ Fill Timeout Description }}

```yaml
Type: Int32
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -oAuth2
Parameter description

```yaml
Type: SwitchParameter
Parameter Sets: oAuth
Aliases: oAuth

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Graph
Parameter description

```yaml
Type: SwitchParameter
Parameter Sets: Graph
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Email
Parameter description

```yaml
Type: IDictionary
Parameter Sets: Grouped
Aliases: EmailParameters

Required: False
Position: Named
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES
General notes

## RELATED LINKS
