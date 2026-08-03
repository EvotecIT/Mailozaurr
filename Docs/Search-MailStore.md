---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Search-MailStore
## SYNOPSIS
Searches mail-store metadata or selected message content.

Uses OfficeIMO.Email bounded summary search by default. Supplying Term enables resumable content search across selected semantic fields.

## SYNTAX
### Metadata (Default)
```powershell
Search-MailStore [-InputObject] <Object> [-FolderId <string>] [-IncludeDescendants] [-IncludeAssociatedItems] [-IncludeOrphanedItems] [-ItemKind <OutlookItemKind>] [-SubjectContains <string>] [-SenderContains <string>] [-Since <DateTimeOffset>] [-Before <DateTimeOffset>] [-HasAttachments <Boolean>] [-IsRead <Boolean>] [-MaxItemsScanned <int>] [-MaxResults <int>] [<CommonParameters>]
```

### Content
```powershell
Search-MailStore [-InputObject] <Object> -Term <string[]> [-FolderId <string>] [-IncludeDescendants] [-IncludeAssociatedItems] [-IncludeOrphanedItems] [-ItemKind <OutlookItemKind>] [-SubjectContains <string>] [-SenderContains <string>] [-Since <DateTimeOffset>] [-Before <DateTimeOffset>] [-HasAttachments <Boolean>] [-IsRead <Boolean>] [-MaxItemsScanned <int>] [-MaxResults <int>] [-Fields <EmailStoreContentSearchFields>] [-MatchMode <EmailStoreContentMatchMode>] [-SnippetCharacters <int>] [-ResumeFrom <EmailStoreContentSearchCheckpoint>] [-ResultsOnly] [<CommonParameters>]
```

## DESCRIPTION
Searches mail-store metadata or selected message content.

Uses OfficeIMO.Email bounded summary search by default. Supplying Term enables resumable content search across selected semantic fields.

## EXAMPLES

### EXAMPLE 1
```powershell
Search-MailStore -Before 'Value'
```


### EXAMPLE 2
```powershell
Search-MailStore -Term @('Value')
```


## PARAMETERS

### -Before
Exclusive upper timestamp bound.

```yaml
Type: DateTimeOffset
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Fields
Semantic fields searched when Term is supplied.

```yaml
Type: EmailStoreContentSearchFields
Parameter Sets: Content
Aliases: None
Possible values: None, Subject, Sender, Recipients, TextBody, HtmlBody, RtfBody, Bodies, AttachmentNames, All

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FolderId
Optional stable folder identifier.

```yaml
Type: String
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HasAttachments
Optional declared attachment-presence filter.

```yaml
Type: Boolean
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeAssociatedItems
Includes folder-associated information items.

```yaml
Type: SwitchParameter
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeDescendants
Includes descendants of FolderId.

```yaml
Type: SwitchParameter
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeOrphanedItems
Includes recoverable items absent from normal folder tables.

```yaml
Type: SwitchParameter
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
An Import-MailData result containing a store, or a native EmailStoreSession.

```yaml
Type: Object
Parameter Sets: Metadata, Content
Aliases: Store
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -IsRead
Optional read-state filter.

```yaml
Type: Boolean
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ItemKind
Optional typed Outlook item classification.

```yaml
Type: OutlookItemKind
Parameter Sets: Metadata, Content
Aliases: None
Possible values: Unknown, Message, Appointment, Contact, Task, Journal, Note, DistributionList

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MatchMode
Whether every term or any term must match.

```yaml
Type: EmailStoreContentMatchMode
Parameter Sets: Content
Aliases: None
Possible values: AnyTerm, AllTerms

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxItemsScanned
Maximum item references inspected.

```yaml
Type: Int32
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxResults
Maximum matches returned.

```yaml
Type: Int32
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResultsOnly
Writes content-search matches instead of the report containing completion and resume state.

```yaml
Type: SwitchParameter
Parameter Sets: Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResumeFrom
Checkpoint returned by a previous bounded content-search batch.

```yaml
Type: EmailStoreContentSearchCheckpoint
Parameter Sets: Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SenderContains
Optional case-insensitive sender name or address fragment.

```yaml
Type: String
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Since
Inclusive lower timestamp bound.

```yaml
Type: DateTimeOffset
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SnippetCharacters
Maximum characters retained in a content-search snippet.

```yaml
Type: Int32
Parameter Sets: Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubjectContains
Optional case-insensitive subject fragment.

```yaml
Type: String
Parameter Sets: Metadata, Content
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Term
Terms used for bounded content search.

```yaml
Type: String[]
Parameter Sets: Content
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

- `System.Object`

## OUTPUTS

- `OfficeIMO.Email.Store.EmailStoreSearchResult`
- `OfficeIMO.Email.Store.EmailStoreContentSearchReport`
- `OfficeIMO.Email.Store.EmailStoreContentSearchResult`

## RELATED LINKS

- None
