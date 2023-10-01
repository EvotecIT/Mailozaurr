function Convert-CloudflareToGoogleDane {
    [CmdletBinding()]
    param(
        [parameter(Mandatory)][string] $DaneRecord
    )
    # Remove the prefix '\# 35 '
    $cloudflareOutput = $DaneRecord -replace '^\\# 35 ', ''

    # Split the string into an array of strings, each containing two characters
    $splitOutput = $cloudflareOutput -split ' '

    # The first three elements represent the certificate usage, selector, and matching type
    # Convert them to integers to remove leading zeros, then back to strings
    $certificateFields = ($splitOutput[0..2] | ForEach-Object { [int]"0x$_" }) -join ' '

    # The remaining elements represent the certificate association data
    $certificateData = $splitOutput[3..($splitOutput.Length - 1)] -join ''

    # Combine the certificate fields and certificate data into the desired format
    $googleFormatOutput = "$certificateFields $certificateData"

    # Now $googleFormatOutput will have the desired format

    $googleFormatOutput
}