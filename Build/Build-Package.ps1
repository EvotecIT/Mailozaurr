Clear-Host

Invoke-DotNetReleaseBuild -ProjectPath "$PSScriptRoot\..\Sources\Mailozaurr" -CertificateThumbprint '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703'
Invoke-DotNetReleaseBuild -ProjectPath "$PSScriptRoot\..\Sources\Mailozaurr.Msg" -CertificateThumbprint '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703'
