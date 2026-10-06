---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailMessage
## SYNOPSIS
Reads messages from local email files or offline archives.

Returns bodies, recipient addresses and attachment descriptions without leaving a source open. First limits matching messages across all input paths. MaxItemsScanned limits summaries examined per source and emits a warning when reached. Payload operations require the original source to remain unchanged.

## SYNTAX
### Store (Default)
```powershell
Get-MailMessage -InputObject <Object> [-Folder <string>] [-Recurse] [-SubjectContains <string>] [-SenderContains <string>] [-Since <DateTimeOffset>] [-Before <DateTimeOffset>] [-First <int>] [-MaxItemsScanned <int>] [<CommonParameters>]
```

### Path
```powershell
Get-MailMessage [-Path] <string[]> [-Folder <string>] [-Recurse] [-SubjectContains <string>] [-SenderContains <string>] [-Since <DateTimeOffset>] [-Before <DateTimeOffset>] [-First <int>] [-MaxItemsScanned <int>] [<CommonParameters>]
```

## DESCRIPTION
Reads messages from local email files or offline archives.

Returns bodies, recipient addresses and attachment descriptions without leaving a source open. First limits matching messages across all input paths. MaxItemsScanned limits summaries examined per source and emits a warning when reached. Payload operations require the original source to remain unchanged.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailMessage -InputObject 'Value'
```


### EXAMPLE 2
```powershell
Get-MailMessage -Path @('C:\Path')
```


## PARAMETERS

### -Before
Exclusive received-date upper bound, falling back to sent date.

```yaml
Type: DateTimeOffset
Parameter Sets: Store, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -First
Maximum matching messages across all input sources. Default: 1000.

```yaml
Type: Int32
Parameter Sets: Store, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Folder
Exact folder path or a unique folder name.

```yaml
Type: String
Parameter Sets: Store, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Store scope supplied by Invoke-MailStore.

```yaml
Type: Object
Parameter Sets: Store
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaxItemsScanned
Maximum summaries examined per source. Default: 1000000.

```yaml
Type: Int32
Parameter Sets: Store, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Local email or archive paths; wildcard patterns are supported.

```yaml
Type: String[]
Parameter Sets: Path
Aliases: FullName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recurse
Includes descendants of Folder.

```yaml
Type: SwitchParameter
Parameter Sets: Store, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SenderContains
Case-insensitive sender name or address fragment.

```yaml
Type: String
Parameter Sets: Store, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Since
Inclusive received-date lower bound, falling back to sent date.

```yaml
Type: DateTimeOffset
Parameter Sets: Store, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubjectContains
Case-insensitive subject fragment.

```yaml
Type: String
Parameter Sets: Store, Path
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

- `System.Object`

## OUTPUTS

- `OfficeIMO.Email.EmailMessage`

## RELATED LINKS

- None
