function Connect-IMAP {
    [cmdletBinding(DefaultParameterSetName = 'Credential')]
    param(
        [Parameter(ParameterSetName = 'oAuth2')]
        [Parameter(ParameterSetName = 'Credential')]
        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(Mandatory)][string] $Server,

        [Parameter(ParameterSetName = 'oAuth2')]
        [Parameter(ParameterSetName = 'Credential')]
        [Parameter(ParameterSetName = 'ClearText')]
        [int] $Port = '993',

        [Parameter(ParameterSetName = 'ClearText', Mandatory)][string] $UserName,
        [Parameter(ParameterSetName = 'ClearText', Mandatory)][string] $Password,

        [Parameter(ParameterSetName = 'oAuth2')]
        [Parameter(ParameterSetName = 'Credential')][System.Management.Automation.PSCredential] $Credential,

        [Parameter(ParameterSetName = 'oAuth2')]
        [Parameter(ParameterSetName = 'Credential')]
        [Parameter(ParameterSetName = 'ClearText')]
        [MailKit.Security.SecureSocketOptions] $Options = [MailKit.Security.SecureSocketOptions]::Auto,

        [Parameter(ParameterSetName = 'oAuth2')]
        [Parameter(ParameterSetName = 'Credential')]
        [Parameter(ParameterSetName = 'ClearText')]
        [int] $TimeOut = 120000,

        [Parameter(ParameterSetName = 'oAuth2')]
        [switch] $oAuth2
    )
    $Client = [MailKit.Net.Imap.ImapClient]::new()
    try {
        $Client.Connect($Server, $Port, $Options)
    } catch {
        Write-Warning "Connect-IMAP - Unable to connect $($_.Exception.Message)"
        return
    }

    <#
    void Connect(string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void Connect(System.Net.Sockets.Socket socket, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void Connect(System.IO.Stream stream, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void Connect(uri uri, System.Threading.CancellationToken cancellationToken)
    void Connect(string host, int port, bool useSsl, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(string host, int port, bool useSsl, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(System.Net.Sockets.Socket socket, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(System.IO.Stream stream, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    #>
    if ($Client.TimeOut -ne $TimeOut) {
        $Client.TimeOut = $Timeout
    }
    if ($Client.IsConnected) {
        if ($oAuth2.IsPresent) {
            $Authorization = ConvertFrom-OAuth2Credential -Credential $Credential
            $SaslMechanismOAuth2 = [MailKit.Security.SaslMechanismOAuth2]::new($Authorization.UserName, $Authorization.Token)
            try {
                $Client.Authenticate($SaslMechanismOAuth2)
            } catch {
                Write-Warning "Connect-POP - Unable to authenticate via oAuth $($_.Exception.Message)"
                return
            }
        } elseif ($UserName -and $Password) {
            try {
                $Client.Authenticate($UserName, $Password)
            } catch {
                Write-Warning "Connect-IMAP - Unable to authenticate $($_.Exception.Message)"
                return
            }
        } else {
            try {
                $Client.Authenticate($Credential)
            } catch {
                Write-Warning "Connect-IMAP - Unable to authenticate $($_.Exception.Message)"
                return
            }
        }
    } else {
        return
    }
    if ($Client.IsAuthenticated) {
        [ordered] @{
            Uri                      = $Client.SyncRoot.Uri                      #: pops: / / pop.gmail.com:995 /
            AuthenticationMechanisms = $Client.SyncRoot.AuthenticationMechanisms #: { }
            Capabilities             = $Client.SyncRoot.Capabilities             #: Expire, LoginDelay, Pipelining, ResponseCodes, Top, UIDL, User
            Stream                   = $Client.SyncRoot.Stream                   #: MailKit.Net.Pop3.Pop3Stream
            State                    = $Client.SyncRoot.State                    #: Transaction
            IsConnected              = $Client.SyncRoot.IsConnected              #: True
            ApopToken                = $Client.SyncRoot.ApopToken                #:
            ExpirePolicy             = $Client.SyncRoot.ExpirePolicy             #: 0
            Implementation           = $Client.SyncRoot.Implementation           #:
            LoginDelay               = $Client.SyncRoot.LoginDelay               #: 300
            IsAuthenticated          = $Client.IsAuthenticated
            IsSecure                 = $Client.IsSecure
            Data                     = $Client
            Count                    = $Client.Count
        }
    }
    <#
    void Authenticate(MailKit.Security.SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken)
    void Authenticate(System.Text.Encoding encoding, System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
    void Authenticate(System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
    void Authenticate(System.Text.Encoding encoding, string userName, string password, System.Threading.CancellationToken cancellationToken)
    void Authenticate(string userName, string password, System.Threading.CancellationToken cancellationToken)
    void IMailService.Authenticate(System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
    void IMailService.Authenticate(System.Text.Encoding encoding, System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
    void IMailService.Authenticate(System.Text.Encoding encoding, string userName, string password, System.Threading.CancellationToken cancellationToken)
    void IMailService.Authenticate(string userName, string password, System.Threading.CancellationToken cancellationToken)
    void IMailService.Authenticate(MailKit.Security.SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken)
    #>

    <#
    -------------------
    System.Threading.Tasks.Task AuthenticateAsync(MailKit.Security.SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken)
    System.Threading.Tasks.Task AuthenticateAsync(System.Text.Encoding encoding, System.Net.ICredentials credentials, System.Threading.CancellationToken cancellati
    onToken)
    System.Threading.Tasks.Task AuthenticateAsync(System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
    System.Threading.Tasks.Task AuthenticateAsync(System.Text.Encoding encoding, string userName, string password, System.Threading.CancellationToken cancellationT
    oken)
    System.Threading.Tasks.Task AuthenticateAsync(string userName, string password, System.Threading.CancellationToken cancellationToken)
    System.Threading.Tasks.Task IMailService.AuthenticateAsync(System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken)
    System.Threading.Tasks.Task IMailService.AuthenticateAsync(System.Text.Encoding encoding, System.Net.ICredentials credentials, System.Threading.CancellationTok
    en cancellationToken)
    System.Threading.Tasks.Task IMailService.AuthenticateAsync(System.Text.Encoding encoding, string userName, string password, System.Threading.CancellationToken
    cancellationToken)
    System.Threading.Tasks.Task IMailService.AuthenticateAsync(string userName, string password, System.Threading.CancellationToken cancellationToken)
    System.Threading.Tasks.Task IMailService.AuthenticateAsync(MailKit.Security.SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken)
    #>

    #$Client.GetMessageSizes
}