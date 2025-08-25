Describe 'ConvertTo-GraphCertificateCredential cmdlet' {
    It 'Cmdlet derives from AsyncPSCmdlet' {
        $base = [Mailozaurr.PowerShell.CmdletConvertToGraphCertificateCredential].BaseType
        $base.FullName | Should -Be 'Mailozaurr.PowerShell.AsyncPSCmdlet'
    }
}

