Describe 'Remove-GraphMessageAttachment' {
    It 'Clears attachments on GraphMessage' {
        $file = Join-Path $TestDrive 'file.txt'
        'b' | Set-Content -Path $file
        $graphMsg = [Mailozaurr.GraphMessage]::new()
        $graphMsg.Subject = 's'
        $graphMsg.Body = [Mailozaurr.GraphContent]::new()
        $graphMsg.Attachments = @([Mailozaurr.GraphAttachment]::FromFile($file))
        $result = Remove-GraphMessageAttachment -Message $graphMsg
        $null -eq $result.Attachments | Should -BeTrue
    }
}
