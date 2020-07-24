Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ExcelReport = "$PSScriptRoot\Output\MailSystemSummary.xlsx"
$HTMLReport = "$PSScriptRoot\Output\MailSystemSummary.html"

$Domains = @(
    'evotec.pl'
    'evotec.xyz'
    'google.com'
    'gmail.com'
    'microsoft.com'
)

$MXRecords = Find-MxRecord -DomainName $Domains
$DMARCRecords = Find-DMARCRecord -DomainName $Domains
$SPFRecords = Find-SPFRecord -DomainName $Domains
$DKIMRecords = Find-DKIMRecord -DomainName $Domains

# Export to Excel
$MXRecords | ConvertTo-Excel -FilePath $ExcelReport -WorksheetName 'MX Records' -AutoFilter -AutoFit
$DMARCRecords | ConvertTo-Excel -FilePath $ExcelReport -WorksheetName 'DMARC Records' -AutoFilter -AutoFit
$SPFRecords | ConvertTo-Excel -FilePath $ExcelReport -WorksheetName 'SPF Records' -AutoFilter -AutoFit
$DKIMRecords | ConvertTo-Excel -FilePath $ExcelReport -WorksheetName 'DKIM Records' -AutoFilter -AutoFit -OpenWorkBook

# Export to HTML
New-HTML {
    New-HTMLSection -Invisible {
        New-HTMLSection -HeaderText 'MX' {
            New-HTMLTable -DataTable $MXRecords -Filtering
        }
        New-HTMLSection -HeaderText 'SPF' {
            New-HTMLTable -DataTable $SPFRecords {
                New-TableCondition -Name 'SPF' -ComparisonType string -Operator like -Value '*-all*' -BackgroundColor Green -Color White
                New-TableCondition -Name 'SPF' -ComparisonType string -Operator like -Value '*~all*' -BackgroundColor Yellow -Color Black
                New-TableCondition -Name 'SPF' -ComparisonType string -Operator like -Value '*\+all*' -BackgroundColor Red -Color White
                New-TableCondition -Name 'Count' -ComparisonType number -Operator gt -Value 1 -BackgroundColor Red -Color White
                New-TableCondition -Name 'Count' -ComparisonType number -Operator lt -Value 1 -BackgroundColor Red -Color White
                New-TableCondition -Name 'Count' -ComparisonType number -Operator eq -Value 1 -BackgroundColor Green -Color White
            }  -Filtering
        }
    }
    New-HTMLSection -Invisible {
        New-HTMLSection -HeaderText 'DKIM' {
            New-HTMLTable -DataTable $DKIMRecords -Filtering -WordBreak break-all {
                New-TableCondition -Name 'Count' -ComparisonType number -Operator gt -Value 1 -BackgroundColor Red -Color White
                New-TableCondition -Name 'Count' -ComparisonType number -Operator lt -Value 1 -BackgroundColor Red -Color White
                New-TableCondition -Name 'Count' -ComparisonType number -Operator eq -Value 1 -BackgroundColor Green -Color White
            }
        }
        New-HTMLSection -HeaderText 'DMARC' {
            New-HTMLTable -DataTable $DMARCRecords -Filtering {
                New-TableCondition -Name 'Count' -ComparisonType number -Operator gt -Value 1 -BackgroundColor Red -Color White
                New-TableCondition -Name 'Count' -ComparisonType number -Operator lt -Value 1 -BackgroundColor Red -Color White
                New-TableCondition -Name 'Count' -ComparisonType number -Operator eq -Value 1 -BackgroundColor Green -Color White
            }
        }
    }
} -Open -FilePath $HTMLReport -Online