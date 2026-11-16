---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Export-GraphMailboxToPst
## SYNOPSIS
Exports an Exchange Online mailbox to one or more PST files through Microsoft Graph.

Reads the mailbox folder tree through Microsoft Graph and streams each message's MIME content straight into a new Unicode PST written by OfficeIMO.Email. The folder hierarchy is kept, and Inbox, Sent Items, Drafts, Junk Email and Outbox keep their Outlook roles. The mailbox is only read; nothing in it is changed.

SplitSizeMB starts a new numbered part (name.part001.pst, name.part002.pst, ...) before an item would take the current part over the size. Each part is reopened and its item count checked, and the report lists every part with its size and SHA-256 hash.

Mail is always exported. IncludeContacts, IncludeCalendar and IncludeTasks add contacts, calendar events and Microsoft To Do tasks as typed Outlook items. Recurring events keep their recurrence pattern; modified and cancelled occurrences of a series, and task recurrence, are not carried and are listed as partial items in the report.

By default the export keeps a checkpoint next to the output. If it is interrupted, run the same command with -Resume to continue where it stopped without duplicating items. Use -NoCheckpoint to turn this off.

Required Microsoft Graph permissions. Application (app-only) access: Mail.Read, plus Contacts.Read for IncludeContacts, Calendars.Read for IncludeCalendar and Tasks.Read.All for IncludeTasks. Delegated access to the signed-in user's own mailbox: Mail.Read, Contacts.Read, Calendars.Read and Tasks.Read. Delegated access to another mailbox the signed-in user can open: Mail.Read.Shared, Contacts.Read.Shared and Calendars.Read.Shared; Microsoft To Do tasks of another user are only available with application access. To limit an application to selected mailboxes, use RBAC for Applications in Exchange Online or an application access policy.

The PST writer creates its own Deleted Items folder, so the mailbox's Deleted Items is written as a separate folder with the same name.

## SYNTAX
### __AllParameterSets
```powershell
Export-GraphMailboxToPst [-Identity] <string> [-OutputPath] <string> [-SplitSizeMB <int>] [-MaxParts <int>] [-IncludeFolder <string[]>] [-ExcludeFolder <string[]>] [-StartDate <DateTime>] [-EndDate <DateTime>] [-IncludeContacts] [-IncludeCalendar] [-IncludeTasks] [-MaxItemSizeMB <int>] [-DisplayName <string>] [-Resume] [-CheckpointPath <string>] [-NoCheckpoint] [-Force] [-StopOnItemError] [-SkipVerification] [-Connection <GraphConnectionInfo>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Exports an Exchange Online mailbox to one or more PST files through Microsoft Graph.

Reads the mailbox folder tree through Microsoft Graph and streams each message's MIME content straight into a new Unicode PST written by OfficeIMO.Email. The folder hierarchy is kept, and Inbox, Sent Items, Drafts, Junk Email and Outbox keep their Outlook roles. The mailbox is only read; nothing in it is changed.

SplitSizeMB starts a new numbered part (name.part001.pst, name.part002.pst, ...) before an item would take the current part over the size. Each part is reopened and its item count checked, and the report lists every part with its size and SHA-256 hash.

Mail is always exported. IncludeContacts, IncludeCalendar and IncludeTasks add contacts, calendar events and Microsoft To Do tasks as typed Outlook items. Recurring events keep their recurrence pattern; modified and cancelled occurrences of a series, and task recurrence, are not carried and are listed as partial items in the report.

By default the export keeps a checkpoint next to the output. If it is interrupted, run the same command with -Resume to continue where it stopped without duplicating items. Use -NoCheckpoint to turn this off.

Required Microsoft Graph permissions. Application (app-only) access: Mail.Read, plus Contacts.Read for IncludeContacts, Calendars.Read for IncludeCalendar and Tasks.Read.All for IncludeTasks. Delegated access to the signed-in user's own mailbox: Mail.Read, Contacts.Read, Calendars.Read and Tasks.Read. Delegated access to another mailbox the signed-in user can open: Mail.Read.Shared, Contacts.Read.Shared and Calendars.Read.Shared; Microsoft To Do tasks of another user are only available with application access. To limit an application to selected mailboxes, use RBAC for Applications in Exchange Online or an application access policy.

The PST writer creates its own Deleted Items folder, so the mailbox's Deleted Items is written as a separate folder with the same name.

## EXAMPLES

### EXAMPLE 1
```powershell
Connect-EmailGraph -ClientId $clientId -CertificatePath './app.pfx' -CertificatePassword $pfxPassword -DirectoryId $tenantId
            Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export' -SplitSizeMB 5120
```

Writes C:\Export\user@contoso.com.part001.pst, part002.pst and so on, and returns the export report.

### EXAMPLE 2
```powershell
Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export\user-2025.pst' -IncludeFolder 'Inbox', 'Sent Items' -StartDate '2025-01-01' -EndDate '2026-01-01' -IncludeContacts -IncludeCalendar
```

IncludeFolder patterns match folder paths such as Inbox\Projects and also select the folder's subfolders.

### EXAMPLE 3
```powershell
Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export' -SplitSizeMB 5120 -Resume
```

Run with the same parameters as the interrupted export.

## PARAMETERS

### -CheckpointPath
Checkpoint file. Defaults to name.checkpoint next to the output.

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

### -Connection
Graph connection from Connect-EmailGraph. Defaults to the current session.

```yaml
Type: GraphConnectionInfo
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisplayName
PST display name shown in Outlook. Defaults to the mailbox identity.

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

### -EndDate
Exports messages received before this time, and events that start before it.

```yaml
Type: DateTime
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExcludeFolder
Folder path patterns to leave out, such as 'Deleted Items' or 'Junk Email'. A matching folder's subfolders are left out too.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Force
Replaces existing PST files and discards an existing checkpoint.

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

### -Identity
User principal name, SMTP address or object id of the mailbox to export.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: UserPrincipalName, Mailbox
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeCalendar
Adds every calendar in the mailbox.

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

### -IncludeContacts
Adds the default contacts folder and its subfolders.

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

### -IncludeFolder
Folder path patterns to export, such as 'Inbox' or 'Inbox\Projects*'. A matching folder's subfolders are included.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeTasks
Adds Microsoft To Do task lists.

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

### -MaxItemSizeMB
Largest message downloaded, in megabytes. Larger messages are skipped and listed in the report.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxParts
Largest number of parts. The export stops with an error if it needs more.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoCheckpoint
Turns off checkpoints, so an interrupted export cannot be resumed.

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

### -OutputPath
Destination PST file, or a directory in which the PST is named after the mailbox.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Path, DestinationPath
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Resume
Continues an interrupted export from its checkpoint.

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

### -SkipVerification
Skips reopening each part to check its item count.

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

### -SplitSizeMB
Largest size of one PST part in megabytes. Without it a single PST is written.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartDate
Exports messages received on or after this time, and events that end on or after it.

```yaml
Type: DateTime
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StopOnItemError
Stops at the first item that cannot be read or written instead of listing it in the report.

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

- `None`

## OUTPUTS

- `Mailozaurr.MailboxExport.GraphMailboxPstExportReport`

## RELATED LINKS

- None
