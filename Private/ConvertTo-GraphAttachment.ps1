function ConvertTo-GraphAttachment {
    [CmdletBinding()]
    param(
        [string[]] $Attachment
    )
    foreach ($A in $Attachment) {
        if ($A -and (Test-Path -LiteralPath $A -ErrorAction SilentlyContinue)) {
            try {
                $ItemInformation = Get-Item -LiteralPath $A -ErrorAction Stop
            } catch {
                Write-Warning -Message "ConvertTo-GraphAttachment: Attachment '$A' processing error. Error: $($_.Exception.Message)"
            }
            if ($ItemInformation) {
                try {
                    $File = [system.io.file]::ReadAllBytes($A)
                    $Bytes = [System.Convert]::ToBase64String($File)
                    @{
                        '@odata.type'  = '#microsoft.graph.fileAttachment'
                        'name'         = $ItemInformation.Name
                        'contentBytes' = $Bytes
                    }
                } catch {
                    Write-Warning -Message "ConvertTo-GraphAttachment: Attachment '$A' reading error. Error: $($_.Exception.Message)"
                }
            }
        } else {
            Write-Warning -Message "ConvertTo-GraphAttachment: Attachment '$A' not found or not accessible. Skipping attachment."
        }
    }
}