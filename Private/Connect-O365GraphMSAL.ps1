function Connect-O365GraphMSAL {
    [cmdletBinding()]
    param(
        [string][alias('ClientSecret')] $ApplicationKey
    )
    @{'Authorization' = "Bearer $ApplicationKey" }
}