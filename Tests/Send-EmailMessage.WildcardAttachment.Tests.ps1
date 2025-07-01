Describe 'Send-EmailMessage - Wildcard Attachments' {
    It 'Expands wildcard file patterns' {
        $dir = Join-Path $TestDrive 'wc'
        New-Item -ItemType Directory -Path $dir | Out-Null
        $f1 = Join-Path $dir 'file1.txt'
        $f2 = Join-Path $dir 'file2.txt'
        'a' | Set-Content -Path $f1
        'b' | Set-Content -Path $f2

        $cmdlet = [Mailozaurr.PowerShell.CmdletSendEmailMessage]::new()
        $method = $cmdlet.GetType().GetMethod('FilterExistingPaths', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        $result = $method.Invoke($cmdlet, @([object[]]@("$dir/*.txt"), 'Attachment'))

        $result.Count | Should -Be 2
        ($result | ForEach-Object { $_.FullName }) | Should -Contain $f1
        ($result | ForEach-Object { $_.FullName }) | Should -Contain $f2
    }
}
