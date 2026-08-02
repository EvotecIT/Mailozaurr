---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# New-GraphEvent
## SYNOPSIS
Creates a new calendar event via Microsoft Graph.

## SYNTAX
### Event
```powershell
New-GraphEvent -UserPrincipalName <string> -Event <GraphEvent> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Builder
```powershell
New-GraphEvent -UserPrincipalName <string> -EventBuilder <GraphEventBuilder> [-Connection <GraphConnectionInfo>] [-TimeoutSeconds <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates a new calendar event via Microsoft Graph.

## EXAMPLES

### EXAMPLE 1
```powershell
New-GraphEvent -UserPrincipalName 'Name' -EventBuilder 'Value'
```


### EXAMPLE 2
```powershell
New-GraphEvent -UserPrincipalName 'Name' -Event 'Value'
```


## PARAMETERS

### -Connection
Graph connection context for the request.

```yaml
Type: GraphConnectionInfo
Parameter Sets: Event, Builder
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Event
Event object to create.

```yaml
Type: GraphEvent
Parameter Sets: Event
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventBuilder
Builder used to construct the event.

```yaml
Type: GraphEventBuilder
Parameter Sets: Builder
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryCount
Number of retry attempts on failure.

```yaml
Type: Int32
Parameter Sets: Event, Builder
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
Parameter Sets: Event, Builder
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
Parameter Sets: Event, Builder
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserPrincipalName
User principal name owning the calendar.

```yaml
Type: String
Parameter Sets: Event, Builder
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

- `Mailozaurr.GraphEvent`

## RELATED LINKS

- None
