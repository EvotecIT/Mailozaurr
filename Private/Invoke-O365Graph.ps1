function Invoke-O365Graph {
    [cmdletBinding()]
    param(
        [uri] $PrimaryUri = 'https://graph.microsoft.com/v1.0',
        [uri] $Uri,
        [alias('Authorization')][System.Collections.IDictionary] $Headers,
        [validateset('GET', 'DELETE', 'POST')][string] $Method = 'GET',
        [string] $ContentType = 'application/json',
        [switch] $FullUri,
        [switch] $MgGraphRequest
    )
    $RestSplat = @{
        Headers     = $Headers
        Method      = $Method
        ContentType = $ContentType
    }
    if ($FullUri) {
        $RestSplat.Uri = $Uri
    } else {
        $RestSplat.Uri = -join ($PrimaryUri, $Uri)
    }
    try {
        if ($MgGraphRequest) {
            $OutputQuery = Invoke-MgGraphRequest @RestSplat -Verbose:$false
        } else {
            $OutputQuery = Invoke-RestMethod @RestSplat -Verbose:$false
        }
        if ($Method -eq 'GET') {
            if ($OutputQuery.value) {
                foreach ($Mail in $OutputQuery.value) {
                    [PSCustomObject] $Mail
                }
            }
            if ($OutputQuery.'@odata.nextLink') {
                $RestSplat.Uri = $OutputQuery.'@odata.nextLink'
                $MoreData = Invoke-O365Graph @RestSplat -FullUri
                if ($MoreData) {
                    $MoreData
                }
            }
        } else {
            return $true
        }
    } catch {
        $RestError = $_.ErrorDetails.Message
        if ($RestError) {
            try {
                $ErrorMessage = ConvertFrom-Json -InputObject $RestError
                $ErrorMy = -join ('JSON Error:' , $ErrorMessage.error.code, ' ', $ErrorMessage.error.message, ' Additional Error: ', $_.Exception.Message)
                Write-Warning $ErrorMy
            } catch {
                Write-Warning $_.Exception.Message
            }
        } else {
            Write-Warning $_.Exception.Message
        }
        if ($Method -ne 'GET') {
            return $false
        }
    }
}