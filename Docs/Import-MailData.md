---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Import-MailData
## SYNOPSIS
Opens an email-data artifact through its OfficeIMO.Email owner.

Detects EML, MSG, OFT, TNEF, ICS, VCF, PST, OST, OLM, EMLX, Mbox, Maildir, Apple Mail directories, and Outlook Offline Address Book data. The returned owner result must be closed when it contains a store, address-book session, or streaming email content.

## SYNTAX
### __AllParameterSets
```powershell
Import-MailData [-InputPath] <string> [-ExpectedKind <EmailDataArtifactKind>] [-UseStreamingEmailReader] [-EmailReaderOptions <EmailReaderOptions>] [-ContentLineReaderOptions <ContentLineReaderOptions>] [-StoreReaderOptions <EmailStoreReaderOptions>] [-AddressBookReaderOptions <OfflineAddressBookReaderOptions>] [<CommonParameters>]
```

## DESCRIPTION
Opens an email-data artifact through its OfficeIMO.Email owner.

Detects EML, MSG, OFT, TNEF, ICS, VCF, PST, OST, OLM, EMLX, Mbox, Maildir, Apple Mail directories, and Outlook Offline Address Book data. The returned owner result must be closed when it contains a store, address-book session, or streaming email content.

## EXAMPLES

### EXAMPLE 1
```powershell
Import-MailData -InputPath 'C:\Path'
```


## PARAMETERS

### -AddressBookReaderOptions
Optional bounded policy for Outlook Offline Address Book artifacts.

```yaml
Type: OfflineAddressBookReaderOptions
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContentLineReaderOptions
Optional bounded policy for ICS and VCF content-line artifacts.

```yaml
Type: ContentLineReaderOptions
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EmailReaderOptions
Optional bounded policy for individual EML, MSG, OFT, or TNEF artifacts.

```yaml
Type: EmailReaderOptions
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExpectedKind
Optional expected owner for ambiguous or extension-free input.

```yaml
Type: EmailDataArtifactKind
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Unknown, EmailDocument, Calendar, Contact, Store, OfflineAddressBook

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputPath
Path to one supported email-data file or mailbox directory.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Path, FullName
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -StoreReaderOptions
Optional bounded policy for PST, OST, OLM, EMLX, Mbox, and mailbox-directory stores.

```yaml
Type: EmailStoreReaderOptions
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseStreamingEmailReader
Uses file-backed streaming content for individual email artifacts.

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

- `System.String`

## OUTPUTS

- `OfficeIMO.Email.Data.EmailDataOpenResult`

## RELATED LINKS

- None
