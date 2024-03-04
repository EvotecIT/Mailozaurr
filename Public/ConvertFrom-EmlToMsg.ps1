function ConvertFrom-EmlToMsg {
    <#
    .SYNOPSIS
    Convert .eml file to .msg file

    .DESCRIPTION
    Convert .eml file to .msg file

    .PARAMETER InputPath
    Path to the .eml file

    .PARAMETER OutputPath
    Path to the .msg file

    .EXAMPLE
    ConvertFrom-EmlToMsg -InputPath 'C:\Temp\Sample.eml' -OutputPath 'C:\Temp\Sample.msg'

    .NOTES
    General notes
    #>
    [cmdletbinding()]
    param(
        [parameter(Mandatory)][alias('FilePath')][string] $InputPath,
        [parameter(Mandatory)][string] $OutputPath
    )
    if ($InputPath -and (Test-Path -LiteralPath $InputPath)) {
        try {
            [MsgKit.Converter]::ConvertEmlToMsg($InputPath, $OutputPath)
            [PSCustomObject] @{
                Status       = $true
                InputPath    = $InputPath
                OutputPath   = $OutputPath
                ErrorMessage = ''
            }
        } catch {
            Write-Warning -Message "ConvertFrom-EmlToMsg - Error converting $FilePath to $OutputPath. Error: $($_.Exception.Message)"
            [PSCustomObject] @{
                Status       = $false
                InputPath    = $InputPath
                OutputPath   = $OutputPath
                ErrorMessage = $_.Exception.Message
            }
        }
    } else {
        Write-Warning -Message "ConvertFrom-EmlToMsg - File $FilePath doesn't exist."
        [PSCustomObject] @{
            Status       = $false
            InputPath    = $InputPath
            OutputPath   = $OutputPath
            ErrorMessage = $false
        }
    }
}