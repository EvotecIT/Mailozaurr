Describe 'GraphUploadSessionResult' {
    It 'Deserializes uploadUrl correctly' {
        $json = '{"uploadUrl":"https://example.com/upload"}'
        $result = [System.Text.Json.JsonSerializer]::Deserialize($json, [Mailozaurr.GraphUploadSessionResult])
        $result.UploadUrl | Should -Be 'https://example.com/upload'
    }
}
