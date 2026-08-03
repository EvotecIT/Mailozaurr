---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-MailStoreFolder
## SYNOPSIS
Lists folders from an imported PST, OST, OLM, Mbox, EMLX, or mailbox directory.

Returns OfficeIMO.Email folder metadata without decoding message bodies or attachments.

## SYNTAX
### __AllParameterSets
```powershell
Get-MailStoreFolder [-InputObject] <Object> [-Name <string>] [-SpecialFolderKind <EmailStoreSpecialFolderKind>] [-RootOnly] [-ExcludeSearchFolders] [<CommonParameters>]
```

## DESCRIPTION
Lists folders from an imported PST, OST, OLM, Mbox, EMLX, or mailbox directory.

Returns OfficeIMO.Email folder metadata without decoding message bodies or attachments.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-MailStoreFolder -Name 'Name'
```


## PARAMETERS

### -ExcludeSearchFolders
Omits dynamic search folders from the results.

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

### -InputObject
An Import-MailData result containing a store, or a native EmailStoreSession.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: Store
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Name
Optional case-insensitive wildcard applied to folder names.

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

### -RootOnly
Returns only root folders.

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

### -SpecialFolderKind
Optional well-known Outlook folder role.

```yaml
Type: EmailStoreSpecialFolderKind
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Unknown, Root, IpmSubtree, Inbox, Outbox, SentItems, DeletedItems, Drafts, Calendar, Contacts, Tasks, Notes, Journal, JunkEmail, SearchRoot, CommonViews, PersonalViews, Archive, SyncIssues, Conflicts, LocalFailures, ServerFailures, RssFeeds, Reminders, ToDo

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

- `OfficeIMO.Email.Store.EmailStoreFolderInfo`

## RELATED LINKS

- None
