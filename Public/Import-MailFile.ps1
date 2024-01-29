function Import-MailFile {
    <#
    .SYNOPSIS
    Load .msg file as PowerShell object

    .DESCRIPTION
    Load .msg file as PowerShell object

    .PARAMETER InputPath
    Path to the .msg file

    .EXAMPLE
    An example

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][alias('FilePath', 'Path')][string] $InputPath
    )
    if ($InputPath -and (Test-Path -LiteralPath $InputPath)) {
        $Message = [MsgReader.Outlook.Storage+Message]::new($InputPath)
        $Message
    } else {
        Write-Warning -Message "Import-MailFile - File $FilePath doesn't exist."
    }
}