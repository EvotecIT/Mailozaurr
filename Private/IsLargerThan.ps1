function IsLargerThan {
    [CmdletBinding()]
    param(
        [string[]] $FilePath,
        [int] $Size = 4000000
    )
    $AttachmentsSize = 0
    foreach ($A in $FilePath) {
        try {
            $ItemInformation = Get-Item -Path $A -ErrorAction Stop
            $AttachmentsSize += $ItemInformation.Length
        } catch {
            Write-Warning -Message "ConvertTo-GraphAttachment: Attachment '$A' processing error. Error: $($_.Exception.Message)"
        }
    }
    if ($AttachmentsSize -gt $Size) {
        $true
    }
}