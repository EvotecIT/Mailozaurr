function Test-EmailAddress {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][string[]] $EmailAddress
    )
    process {
        foreach ($Email in $EmailAddress) {
            [PSCustomObject] @{
                EmailAddress = $Email
                IsValid      = [EmailValidation.EmailValidator]::Validate($Email)
            }
        }
    }
}