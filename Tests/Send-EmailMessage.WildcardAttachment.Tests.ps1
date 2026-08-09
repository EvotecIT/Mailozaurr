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

    It 'Converts Resolve-Path results to file attachment descriptors' {
        $file = Join-Path $TestDrive 'resolved.txt'
        'content' | Set-Content -LiteralPath $file
        $resolved = Resolve-Path -LiteralPath $file
        $converter = [Mailozaurr.PowerShell.CmdletSendEmailMessage].Assembly.GetType('Mailozaurr.PowerShell.AttachmentInputConverter', $true)
        $method = $converter.GetMethod('Convert', [System.Reflection.BindingFlags] 'NonPublic, Static')
        $arguments = [object[]]::new(1)
        $arguments[0] = [object[]] @($resolved)

        $result = $method.Invoke($null, $arguments)

        $result | Should -HaveCount 1
        $result[0].FilePath | Should -Be $resolved.ProviderPath
    }

    It 'Keeps file-backed Graph inline attachments eligible for upload sessions' {
        $file = Join-Path $TestDrive 'inline.png'
        'content' | Set-Content -LiteralPath $file
        $descriptorType = [Mailozaurr.EmailMessage].Assembly.GetType('Mailozaurr.Definitions.FileAttachmentDescriptor', $true)
        $descriptor = [Activator]::CreateInstance($descriptorType, $file)
        $cmdlet = [Mailozaurr.PowerShell.CmdletSendEmailMessage]::new()
        $method = $cmdlet.GetType().GetMethod('MergeGraphAttachments', [System.Reflection.BindingFlags] 'NonPublic, Static')
        $arguments = [object[]]::new(2)
        $arguments[0] = $null
        $arguments[1] = [object[]] @($descriptor)

        $result = $method.Invoke($null, $arguments)

        $result | Should -HaveCount 1
        $mapped = $result[0]
        $mapped.GetType() | Should -Be $descriptorType
        $mapped | Should -Not -Be $descriptor
        $mapped.ContentDisposition.Disposition | Should -Be 'inline'
        $descriptor.ContentDisposition | Should -BeNullOrEmpty
    }
}
