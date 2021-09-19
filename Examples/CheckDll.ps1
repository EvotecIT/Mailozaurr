Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Type = @(
    [MailKit.Security.SaslMechanismOAuth2]
    [MimeKit.MessagePriority]
    [MimeKit.MimeMessage]
    [MimeKit.InternetAddress]
    [MailKit.Security.SecureSocketOptions]
    [System.Data.DataTable]
    [System.Text.Encoding]
    [Net.ServicePointManager]
    [System.Buffers.BuffersExtensions]
    [System.Buffers.ArrayPool[int]]
)

$Output = foreach ($T in $Type) {
    try {
        $Item = (Get-Item -LiteralPath ($T).Assembly.Location)
        [PSCustomObject] @{
            Name           = $Item.Name
            FullName       = $Item.FullName
            FileVersion    = $Item.VersionInfo.FileVersion
            ProductVersion = $Item.VersionInfo.ProductVersion
            ProductName    = $Item.VersionInfo.ProductName
        }
    } catch {
        Write-Warning -Message "Couldn't process $T. Exception $($_.Exception.message)"
    }
}
$Output | Format-Table *