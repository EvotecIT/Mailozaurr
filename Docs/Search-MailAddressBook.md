---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Search-MailAddressBook
## SYNOPSIS
Searches a bounded, resumable Outlook Offline Address Book.

Returns OfficeIMO.Email's native report with matches, diagnostics, and a checkpoint for the next batch.

## SYNTAX
### __AllParameterSets
```powershell
Search-MailAddressBook [-InputObject] <Object> -Term <string[]> [-Fields <OfflineAddressBookSearchFields>] [-MatchMode <OfflineAddressBookSearchMatchMode>] [-AddressListId <string>] [-ObjectType <OfflineAddressBookObjectType>] [-MaxEntriesScanned <int>] [-MaxResults <int>] [-MaxSearchableCharactersPerEntry <int>] [-SnippetCharacters <int>] [-ResumeFrom <OfflineAddressBookSearchCheckpoint>] [-ResultsOnly] [<CommonParameters>]
```

## DESCRIPTION
Searches a bounded, resumable Outlook Offline Address Book.

Returns OfficeIMO.Email's native report with matches, diagnostics, and a checkpoint for the next batch.

## EXAMPLES

### EXAMPLE 1
```powershell
Search-MailAddressBook -Term @('Value')
```


## PARAMETERS

### -AddressListId
Optional address-list scope.

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

### -Fields
Semantic fields searched.

```yaml
Type: OfflineAddressBookSearchFields
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: None, Names, Addresses, Organization, Phones, PostalAddress, Comment, Membership, All

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Import-MailData result containing an OAB, or a native OAB session.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MatchMode
Whether all terms or any term must match.

```yaml
Type: OfflineAddressBookSearchMatchMode
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: AnyTerm, AllTerms

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxEntriesScanned
Maximum entries scanned in this batch.

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

### -MaxResults
Maximum matches returned in this batch.

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

### -MaxSearchableCharactersPerEntry
Maximum searchable characters decoded from one entry.

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

### -ObjectType
Optional address-entry type.

```yaml
Type: OfflineAddressBookObjectType
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Unknown, Container, MailUser, DistributionList

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResultsOnly
Writes matches instead of the report containing completion and resume state.

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

### -ResumeFrom
Checkpoint from a previous batch with the same search terms and policy.

```yaml
Type: OfflineAddressBookSearchCheckpoint
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SnippetCharacters
Maximum snippet characters.

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

### -Term
One to 32 search terms.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
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

- `OfficeIMO.Email.AddressBook.OfflineAddressBookSearchReport`
- `OfficeIMO.Email.AddressBook.OfflineAddressBookSearchResult`

## RELATED LINKS

- None
