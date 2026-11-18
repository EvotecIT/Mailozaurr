---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Export-GraphMailboxToPst
## SYNOPSIS
Exports an Exchange Online mailbox to one or more PST files through Microsoft Graph.

## SYNTAX
### __AllParameterSets
```powershell
Export-GraphMailboxToPst [-Identity] <string> [-OutputPath] <string> [-SplitSizeMB <int>] [-MaxParts <int>] [-IncludeFolder <string[]>] [-ExcludeFolder <string[]>] [-StartDate <DateTime>] [-EndDate <DateTime>] [-IncludeContacts] [-IncludeCalendar] [-IncludeTasks] [-MaxItemSizeMB <int>] [-DisplayName <string>] [-Resume] [-CheckpointPath <string>] [-NoCheckpoint] [-Force] [-StopOnItemError] [-SkipVerification] [-Connection <GraphConnectionInfo>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Reads the mailbox folder tree through Microsoft Graph and streams each message's MIME content straight into a new Unicode PST written by OfficeIMO.Email. The folder hierarchy is kept, and Inbox, Sent Items, Drafts, Junk Email and Outbox keep their Outlook roles. The mailbox is only read; nothing in it is changed.

SplitSizeMB uses an estimated size to start a new numbered part (name.part001.pst, name.part002.pst, ...). Actual PST sizes can differ, and a part can contain a single item larger than the estimate. By default, the exporter reopens each part and checks its item count. -SkipVerification skips that check. The report always includes each part's size and SHA-256 hash.

Mail is always exported. IncludeContacts, IncludeCalendar and IncludeTasks add contacts, calendar events and Microsoft To Do tasks as typed Outlook items.

The exporter preserves calendar recurrence patterns it can map. If a recurrence pattern is incomplete or unsupported, the PST contains the first occurrence and the report marks the item as partial. Calendar and task attachments, modified and cancelled occurrences of a calendar series, and task recurrence are omitted and reported as partial items.

By default the export keeps a checkpoint next to the output. To continue, add -Resume and keep the original mailbox, destination, split size, filters and item options. If the export stopped at -MaxParts, increase that limit when resuming from a valid checkpoint. The exporter checks each completed part against its stored SHA-256 hash before continuing. Use -NoCheckpoint to turn checkpoints off.

For app-only access through unscoped Microsoft Entra consent, grant Mail.Read for mail, Contacts.Read for IncludeContacts and Calendars.Read for IncludeCalendar. These grants cover every mailbox in the tenant unless an Exchange Application Access Policy scopes them.

For app-only access to selected mailboxes through Exchange Online Application RBAC, assign the corresponding scoped Application Mail.Read, Application Contacts.Read and Application Calendars.Read roles. Remove overlapping unscoped Microsoft Entra grants. Exchange RBAC and Microsoft Entra permissions are additive. IncludeTasks requires the separate Microsoft Graph application permission Tasks.Read.All because Exchange mailbox scopes do not cover Microsoft To Do.

Delegated access to the signed-in user's own mailbox uses Mail.Read, Contacts.Read, Calendars.Read and Tasks.Read as needed. For a shared mailbox the signed-in user can open, use Mail.Read.Shared, Contacts.Read.Shared and Calendars.Read.Shared. Reading another user's Microsoft To Do tasks requires app-only access.

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
Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export\user-2025.pst' -IncludeFolder 'Inbox', 'Sent Items', 'Contacts', 'Calendar' -StartDate '2025-01-01' -EndDate '2026-01-01' -IncludeContacts -IncludeCalendar
```

IncludeFolder patterns match paths such as Inbox\Projects and include the folder's subfolders. Use the actual mailbox folder and calendar names if they differ from the example.

### EXAMPLE 3
```powershell
Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath 'C:\Export' -SplitSizeMB 5120 -Resume
```

Keep the original mailbox, destination, split size, filters and item options. If the export reached its part limit, add a higher -MaxParts value.

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
Folder path patterns to leave out, such as 'Deleted Items' or 'Junk Email'. Patterns apply to mail, contacts, calendars and task lists. A matching folder's subfolders are left out too.

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
Folder path patterns to export, such as 'Inbox' or 'Inbox\Projects*'. Patterns apply to mail, contacts, calendars and task lists. A matching folder's subfolders are included.

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
Largest number of parts. If the export needs more, raise this limit when resuming from a valid checkpoint.

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
Skips reopening each part to check its item count. The SHA-256 hash is still calculated.

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
Estimated size of a PST part in MiB. Actual sizes can exceed the estimate, including when one item exceeds it. Omit this parameter to write a single PST.

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
