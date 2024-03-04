function Convert-CloudflareToGoogleCAA {
    [cmdletbinding()]
    param (
        [string]$hexString
    )

    # Remove the leading hash mark and space, and filter out non-hexadecimal characters
    $cleanedHexString = $hexString.TrimStart("# ") -replace "[^0-9A-Fa-f]", ""

    # Convert the cleaned string into an array of bytes
    $bytes = [byte[]]($cleanedHexString -split '(..)' | Where-Object { $_ } | ForEach-Object { [convert]::ToByte($_, 16) })

    # Skip the initial length byte
    $dataBytes = $bytes[1..($bytes.Length - 1)]

    # Extract the flag byte
    $flag = $dataBytes[0]

    # Extract the tag length and tag
    $tagLength = $dataBytes[1]
    $tag = [System.Text.Encoding]::ASCII.GetString($dataBytes[2..($tagLength + 1)])

    # Extract the value
    $value = [System.Text.Encoding]::ASCII.GetString($dataBytes[($tagLength + 2)..($dataBytes.Length - 1)])

    "$flag $tag $value"
}