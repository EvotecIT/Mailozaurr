# Prepare a local email package for report review

Use Mailozaurr to compose a message with existing report files and save it as EML. The reviewer can inspect recipients, text and attachments before deciding how to deliver it.

```powershell
Import-Module Mailozaurr

$attachments = @('./Output/EventIncidentBrief.pdf', './Output/EventRegister.xlsx')
$message = New-MimeMessage `
    -From 'Reporting <reporting@example.invalid>' `
    -To 'Review team <review@example.invalid>' `
    -Subject 'Incident review package' `
    -TextBody 'Please review the brief and editable event register.' `
    -AttachmentPath $attachments

$message | Save-MimeMessage -Path ./Output/IncidentReview.eml
```

This workflow performs local file operations and needs no SMTP server, OAuth session or mail credentials. It uses Mailozaurr 3.0.2 or later. Create the output directory and attachments before running the snippet.

The [PSWriteOffice report-package recipe](https://github.com/EvotecIT/PSWriteOffice/blob/main/Examples/Integrations/Recipe-Mailozaurr-PdfDelivery.ps1) handles the full sequence: create a sample service PDF or accept existing attachments, save an EML, and optionally send only when `-Send` and SMTP settings are supplied. The accompanying [HTML and Windows event recipes](https://github.com/EvotecIT/PSWriteOffice/tree/main/Examples/Integrations) create Word/PDF briefs and numeric Excel registers that can be attached unchanged.

Keep content generation in OfficeIMO/PSWriteOffice and message composition or delivery in Mailozaurr. The files between them are ordinary attachments; neither module needs a required dependency on the other.
