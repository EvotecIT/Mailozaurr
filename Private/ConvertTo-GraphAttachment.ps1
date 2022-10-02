function ConvertTo-GraphAttachment {
    [CmdletBinding()]
    param(
        [string[]] $Attachment
    )
    foreach ($A in $Attachment) {
        try {
            $ItemInformation = Get-Item -Path $A -ErrorAction Stop
        } catch {
            if ($PSBoundParameters.ErrorAction -eq 'Stop') {
                throw
                return
            } else {
                Write-Warning -Message "ConvertTo-GraphAttachment: Attachment '$A' processing error. Error: $($_.Exception.Message)"
            }
        }
        if ($ItemInformation) {
            $File = [system.io.file]::ReadAllBytes($A)
            $Bytes = [System.Convert]::ToBase64String($File)
            @{
                '@odata.type'  = '#microsoft.graph.fileAttachment'
                #'@odata.type'  = '#Microsoft.OutlookServices.FileAttachment'
                'name'         = $ItemInformation.Name
                #'contentType'  = 'text/plain'
                'contentBytes' = $Bytes
            }
        }
    }
}