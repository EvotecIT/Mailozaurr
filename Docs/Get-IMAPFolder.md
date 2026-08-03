---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Get-IMAPFolder
## SYNOPSIS
Retrieves the IMAP inbox folder and updates message counts for an active IMAP connection.

The Get-IMAPFolder cmdlet opens the inbox folder for the provided ImapConnectionInfo object (from Connect-IMAP), updates message and recent counts, and returns the updated connection info. Use this to refresh folder state or after connecting to an IMAP server.

## SYNTAX
### Root
```powershell
Get-IMAPFolder [[-Client] <ImapConnectionInfo>] [-Root] [<CommonParameters>]
```

### Open
```powershell
Get-IMAPFolder [[-Client] <ImapConnectionInfo>] [[-FolderAccess] <FolderAccess>] [-Path <string>] [<CommonParameters>]
```

## DESCRIPTION
Retrieves the IMAP inbox folder and updates message counts for an active IMAP connection.

The Get-IMAPFolder cmdlet opens the inbox folder for the provided ImapConnectionInfo object (from Connect-IMAP), updates message and recent counts, and returns the updated connection info. Use this to refresh folder state or after connecting to an IMAP server.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-IMAPFolder -Path 'C:\Path'
```


## PARAMETERS

### -Client
The ImapConnectionInfo object representing the active IMAP connection. This is the object returned by Connect-IMAP.

```yaml
Type: ImapConnectionInfo
Parameter Sets: Root, Open
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -FolderAccess
Specifies the folder access mode (ReadOnly or ReadWrite). Default is ReadOnly.

```yaml
Type: FolderAccess
Parameter Sets: Open
Aliases: None
Possible values: None, ReadOnly, ReadWrite

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Folder path to open. Defaults to the inbox when not provided.

```yaml
Type: String
Parameter Sets: Open
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Root
When specified, lists only the top-level folders instead of opening one.

```yaml
Type: SwitchParameter
Parameter Sets: Root
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

- `Mailozaurr.PowerShell.ImapConnectionInfo`: Represents the result of a successful IMAP connection, including the client and connection details.

## OUTPUTS

- `None`

## RELATED LINKS

- CmdletConnectIMAP
- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)
