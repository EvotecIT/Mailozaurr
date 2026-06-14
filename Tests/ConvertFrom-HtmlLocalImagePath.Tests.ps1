Import-Module $PSScriptRoot/../Mailozaurr.psd1 -Force

Describe 'ConvertFrom-HtmlLocalImagePath' {
    It 'Rewrites local image paths to cid references and returns discovered paths' {
        $tmp = New-TemporaryFile
        try {
            $html = "<img src='$tmp'><img src='https://example.com/a.png'>"
            $result = ConvertFrom-HtmlLocalImagePath -Html $html

            $result.Html | Should -BeLike '*cid:*'
            $result.Html | Should -BeLike '*https://example.com/a.png*'
            $result.Paths | Should -Contain $tmp.FullName
        } finally {
            Remove-Item -LiteralPath $tmp.FullName -Force -ErrorAction SilentlyContinue
        }
    }
}
