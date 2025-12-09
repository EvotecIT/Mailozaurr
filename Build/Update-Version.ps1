Import-Module PSPublishModule -Force

Get-ProjectVersion -Path "C:\Support\GitHub\Mailozaurr\Sources" -ExcludeFolders @('C:\Support\GitHub\Mailozaurr\Module\Artefacts') | Format-Table

Set-ProjectVersion -Path "C:\Support\GitHub\Mailozaurr\Sources" -NewVersion "2.0.2" -Verbose -ExcludeFolders @('C:\Support\GitHub\Mailozaurr\Module\Artefacts') #-WhatIf
