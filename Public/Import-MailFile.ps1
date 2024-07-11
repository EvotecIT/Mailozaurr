function Import-MailFile {
    <#
    .SYNOPSIS
    Load .msg or .eml file as PowerShell object

    .DESCRIPTION
    Load .msg or .eml file as PowerShell object

    .PARAMETER InputPath
    Path to the .msg file

    .EXAMPLE
    $Msg = Import-MailFile -FilePath "$PSScriptRoot\Input\TestMessage.msg"
    $Msg | Format-Table

    .EXAMPLE
    $Eml = Import-MailFile -FilePath "$PSScriptRoot\Input\Sample.eml"
    $Eml | Format-Table

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][alias('FilePath', 'Path')][string] $InputPath
    )
    if ($InputPath -and (Test-Path -LiteralPath $InputPath)) {
        Try {
            $Item = Get-Item -LiteralPath $InputPath -ErrorAction Stop
        } catch {
            Write-Warning -Message "Import-MailFile - File $FilePath doesn't exist. Error: $_.Exception.Message"
            return
        }
        try {
            if ($Item.Extension -eq '.msg') {
                $Message = [MsgReader.Outlook.Storage+Message]::new($InputPath)
                $Message
            } elseif ($Item.Extension -eq '.eml') {
                $Message = [MsgReader.Mime.Message]::Load($InputPath)
                $Message
            } else {
                Write-Warning -Message "Import-MailFile - File $FilePath is not a .msg or .eml file."
            }
        } catch {
            Write-Warning -Message "Import-MailFile - File $FilePath is not a .msg or .eml file or another error occured. Error: $_.Exception.Message"
        }
    } else {
        Write-Warning -Message "Import-MailFile - File $FilePath doesn't exist."
    }
}