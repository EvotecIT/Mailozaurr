function Get-POPMessage {
    [alias('Get-POP3Message')]
    [cmdletBinding()]
    param(
        [Parameter()][System.Collections.IDictionary] $Client,
        [int] $Index,
        [int] $Count = 1,
        [switch] $All
    )
    if ($Client -and $Client.Data) {
        if ($All) {
            $Client.Data.GetMessages($Index, $Count)
        } else {
            if ($Index -lt $Client.Data.Count) {
                $Client.Data.GetMessages($Index, $Count)
            } else {
                Write-Warning "Get-POP3Message - Index is out of range. Use index less than $($Client.Data.Count)."
            }
        }
    } else {
        Write-Warning 'Get-POP3Message - Is POP3 connected?'
    }
    <#
    $Client.Data.GetMessage
    MimeKit.MimeMessage GetMessage(int index, System.Threading.CancellationToken cancellationToken, MailKit.ITransferProgress progress)
    MimeKit.MimeMessage IMailSpool.GetMessage(int index, System.Threading.CancellationToken cancellationToken, MailKit.ITransferProgress progress)
    #>
    <#
    $Client.Data.GetMessages
    System.Collections.Generic.IList[MimeKit.MimeMessage] GetMessages(System.Collections.Generic.IList[int] indexes, System.Threading.CancellationToken cancellationToken, MailKit.ITransferProgress progress)
    System.Collections.Generic.IList[MimeKit.MimeMessage] GetMessages(int startIndex, int count, System.Threading.CancellationToken cancellationToken, MailKit.ITransferProgress progress)
    System.Collections.Generic.IList[MimeKit.MimeMessage] IMailSpool.GetMessages(System.Collections.Generic.IList[int] indexes, System.Threading.CancellationTokencancellationToken, MailKit.ITransferProgress progress)
    System.Collections.Generic.IList[MimeKit.MimeMessage] IMailSpool.GetMessages(int startIndex, int count, System.Threading.CancellationToken cancellationToken, MailKit.ITransferProgress progress)
    #>
}