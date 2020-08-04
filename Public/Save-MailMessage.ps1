function Save-MailMessage {
    [cmdletBinding()]
    param(
        [Parameter(ValueFromPipeline, ValueFromPipelineByPropertyName, Position = 0)][PSCustomObject[]] $Message,
        [string] $Path
    )
    Begin {
        $ResolvedPath = Convert-Path -LiteralPath $Path
    }
    Process {
        if (-not $ResolvedPath) {
            return
        }
        foreach ($M in $Message) {
            if ($M -and $M.Body -and $M.Content) {
                Write-Verbose "Processing $($M.changekey)"
                $RandomFileName = [io.path]::GetRandomFileName()
                $RandomFileName = [io.path]::ChangeExtension($RandomFileName, 'html')
                $FilePath = [io.path]::Combine($ResolvedPath, $RandomFileName)
                try {
                    $M.Body.Content | Out-File -FilePath $FilePath -ErrorAction Stop
                } catch {
                    Write-Warning "Save-MailMessage - Coultn't save file to $FilePath. Error: $($_.Exception.Message)"
                }
            }
        }
    }
    End {}
}