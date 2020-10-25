function Test-EmailAddress {
    <#
    .SYNOPSIS
    Checks if email address matches conditions to be valid email address.

    .DESCRIPTION
    Checks if email address matches conditions to be valid email address.

    .PARAMETER EmailAddress
    EmailAddress to check

    .EXAMPLE
    Test-EmailAddress -EmailAddress 'przemyslaw.klys@test'

    .EXAMPLE
    Test-EmailAddress -EmailAddress 'przemyslaw.klys@test.pl'

    .EXAMPLE
    Test-EmailAddress -EmailAddress 'przemyslaw.klys@test','przemyslaw.klys@test.pl'

    .NOTES
    General notes
    #>
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