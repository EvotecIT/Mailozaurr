function Get-MailMessageAttachment {
    <#
    .SYNOPSIS
    Get mail message attachment from Office 365 Graph API using UserPrincipalName and MessageId

    .DESCRIPTION
    Get mail message attachment from Office 365 Graph API using UserPrincipalName and MessageId
    Usually should be used with Get-MailMessage to get MessageId.

    .PARAMETER UserPrincipalName
    UserPrincipalName of the mailbox to get attachments from

    .PARAMETER Id
    MessageId of the message to get attachments from

    .PARAMETER Credential
    Credential parameter is used to securely pass tokens/api keys for Graph API

    .PARAMETER Property
    Property parameter is used to select which properties to return.
    By default if Path is specified, the file is saved and the file object is returned.
    However if Path is not specified, the data is returned from Graph API.

    .PARAMETER MgGraphRequest
    Forces using Invoke-MgGraphRequest internally.
    This allows to use Connect-MgGraph to authenticate and then use Get-MailMessageAttachment without any additional parameters.

    .PARAMETER Path
    Path parameter is used to specify where to save the attachment.

    .EXAMPLE


    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $UserPrincipalName,
        [Parameter(Mandatory)][alias('MessageID')][string] $Id,
        [PSCredential] $Credential,
        [string[]] $Property,
        [switch] $MgGraphRequest,
        [string] $Path
    )

    if ($MgGraphRequest) {
        # do nothing, as we're using Connect-MgGraph
    } else {
        if ($Credential) {
            $AuthorizationData = ConvertFrom-GraphCredential -Credential $Credential
        } else {
            return
        }
        if ($AuthorizationData.ClientID -eq 'MSAL') {
            $Authorization = Connect-O365GraphMSAL -ApplicationKey $AuthorizationData.ClientSecret
        } else {
            $Authorization = Connect-O365Graph -ApplicationID $AuthorizationData.ClientID -ApplicationKey $AuthorizationData.ClientSecret -TenantDomain $AuthorizationData.DirectoryID -Resource https://graph.microsoft.com
        }
    }

    $QueryParameters = [ordered] @{
        select = $Property -join ','
    }
    $joinUriQuerySplat = @{
        BaseUri               = 'https://graph.microsoft.com/v1.0'
        QueryParameter        = $QueryParameters
        RelativeOrAbsoluteUri = "/users/$UserPrincipalName/messages/$Id/attachments"
    }
    Remove-EmptyValue -Hashtable $joinUriQuerySplat -Recursive -Rerun 2

    $Uri = Join-UriQuery @joinUriQuerySplat

    Write-Verbose "Get-MailMessageAttachment - Executing $Uri"

    $OutputData = Invoke-O365Graph -Headers $Authorization -Uri $Uri -Method GET -MGGraphRequest:$MgGraphRequest.IsPresent -FullUri

    if ($OutputData) {
        foreach ($Data in $OutputData) {
            if ($Data.contentBytes -and $Path) {
                try {
                    $PathToFile = [System.IO.Path]::Combine($Path, $Data.Name)
                    $FileStream = [System.IO.FileStream]::new($PathToFile, [System.IO.FileMode]::Create)
                    $AttachedBytes = [System.Convert]::FromBase64String($Data.ContentBytes)
                    $FileStream.Write($AttachedBytes, 0, $AttachedBytes.Length)
                    $FileStream.Close()
                    Get-Item -Path $PathToFile
                } catch {
                    Write-Warning "Get-MailMessageAttachment - Couldn't save file to $Path. Error: $($_.Exception.Message)"
                }
            } else {
                $Data
            }
        }
    } else {
        Write-Verbose "Get-MailMessageAttachment - No data found"
    }
}