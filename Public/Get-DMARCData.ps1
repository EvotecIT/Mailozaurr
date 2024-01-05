function Get-DMARCData {
    [CmdletBinding()]
    param(
        [parameter(Mandatory)][alias('FilePath')][string] $Path
    )
    $Year1970 = Get-Date -Year 1970 -Month 1 -Day 1 00:00:00
    if ($Path -and (Test-Path -LiteralPath $Path)) {
        try {
            [xml]$Report = Get-Content -Path $Path -Raw -ErrorAction Stop
        } catch {
            Write-Warning "Get-DMARCData - Couldn't read file $Path. Error: $($_.Exception.Message)"
            return
        }
        if ($Report.feedback) {
            $DMARCReport = $Report.feedback

            $DateBegin = $Year1970 + ([System.TimeSpan]::fromseconds($DMARCReport.report_metadata.Date_Range.Begin))
            $DateEnd = $Year1970 + ([System.TimeSpan]::fromseconds($DMARCReport.report_metadata.Date_Range.End))

            $OutputData = [ordered] @{
                MetaData        = [ordered] @{
                    OrgName          = $DMARCReport.report_metadata.Org_Name
                    Email            = $DMARCReport.report_metadata.Email
                    ExtraContactInfo = $DMARCReport.report_metadata.Extra_Contact_Info
                    ReportID         = $DMARCReport.report_metadata.Report_ID
                    DateRangeBegin   = $DateBegin # 1580342400
                    DateRangeEnd     = $DateEnd # 1580428799
                }
                PolicyPublished = [ordered] @{
                    Domain = $DMARCReport.policy_published.Domain
                    ADKIM  = $DMARCReport.policy_published.ADKIM
                    ASPF   = $DMARCReport.policy_published.ASPF
                    P      = $DMARCReport.policy_published.P
                    SP     = $DMARCReport.policy_published.SP
                    PCT    = $DMARCReport.policy_published.PCT
                }
                Records         = $null
            }
            $OutputData['Records'] = foreach ($Record in $DMARCReport.record) {
                $Object = [ordered] @{
                    HeaderFrom  = $Record.identifiers.header_from
                    SourceIP    = $Record.Row.source_ip
                    Date        = $DateBegin
                    Count       = $Record.Row.count
                    Disposition = $Record.Row.policy_evaluated.disposition
                    DKIM        = $Record.Row.policy_evaluated.dkim
                    SPF         = $Record.Row.policy_evaluated.spf
                }
                $Count = 0
                foreach ($Dkim in $Record.auth_results.dkim) {
                    $Count++
                    $Object["AuthResultsDKIMDomain$Count"] = $Dkim.domain
                    $Object["AuthResultsDKIMResult$Count"] = $Dkim.result
                    $Object["AuthResultsDKIMSelector$Count"] = $Dkim.selector
                }
                $Count = 0
                foreach ($SPF in $Record.auth_results.spf) {
                    $Count++
                    $Object["AuthResultsSPFDomain$Count"] = $Record.auth_results.spf.domain
                    $Object["AuthResultsSPFResult$Count"] = $Record.auth_results.spf.result
                }
                [PSCustomObject]$Object
            }
        }
        $OutputData
    }
}