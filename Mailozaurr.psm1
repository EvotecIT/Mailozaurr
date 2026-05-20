# to speed up development you can opt into direct build output instead of the Lib folder
$Development = @('1', 'true', 'yes') -contains "$env:MAILOZAURR_DEVELOPMENT".ToLowerInvariant()
$DevelopmentPath = "$PSScriptRoot\Sources\Mailozaurr.PowerShell\bin\Debug"
$DevelopmentFolderCore = 'net8.0'
$DevelopmentFolderDefault = 'net472'
$BinaryModules = @(
    'Mailozaurr.PowerShell.dll'
)
$SkipAssemblyNames = @(
    'System.Management.Automation.dll'
    'System.Management.dll'
)

if ($PSEdition -eq 'Core') {
    $SkipAssemblyNames += @(
        'System.Formats.Asn1.dll'
        'System.Security.Cryptography.Pkcs.dll'
        'System.Security.Cryptography.ProtectedData.dll'
    )
}

# Get public and private function definition files.
$Public = @(Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 -ErrorAction SilentlyContinue -Recurse -File)
$Private = @(Get-ChildItem -Path $PSScriptRoot\Private\*.ps1 -ErrorAction SilentlyContinue -Recurse -File)
$Classes = @(Get-ChildItem -Path $PSScriptRoot\Classes\*.ps1 -ErrorAction SilentlyContinue -Recurse -File)
$Enums = @(Get-ChildItem -Path $PSScriptRoot\Enums\*.ps1 -ErrorAction SilentlyContinue -Recurse -File)

# Get all packaged assembly folders.
$AssemblyFolders = @(Get-ChildItem -Path $PSScriptRoot\Lib -Directory -ErrorAction SilentlyContinue)

if ($Development -and -not (Test-Path $DevelopmentPath)) {
    Write-Warning "Development mode requested, but debug binaries were not found in $DevelopmentPath. Falling back to packaged libraries."
    $Development = $false
}

# Lets find which libraries we need to load
if ($Development) {
    $Framework = 'Core'
    $FrameworkNet = 'Default'
} else {
    $Default = $false
    $Core = $false
    $Standard = $false
    foreach ($A in $AssemblyFolders.Name) {
        if ($A -eq 'Default') {
            $Default = $true
        } elseif ($A -eq 'Core') {
            $Core = $true
        } elseif ($A -eq 'Standard') {
            $Standard = $true
        }
    }
    if ($Standard -and $Core -and $Default) {
        $FrameworkNet = 'Default'
        $Framework = 'Standard'
    } elseif ($Standard -and $Core) {
        $Framework = 'Standard'
        $FrameworkNet = 'Standard'
    } elseif ($Core -and $Default) {
        $Framework = 'Core'
        $FrameworkNet = 'Default'
    } elseif ($Standard -and $Default) {
        $Framework = 'Standard'
        $FrameworkNet = 'Default'
    } elseif ($Standard) {
        $Framework = 'Standard'
        $FrameworkNet = 'Standard'
    } elseif ($Core) {
        $Framework = 'Core'
        $FrameworkNet = ''
    } elseif ($Default) {
        $Framework = ''
        $FrameworkNet = 'Default'
    }
}

$BinaryDev = @(
    if ($Development) {
        foreach ($BinaryModule in $BinaryModules) {
            if ($PSEdition -eq 'Core') {
                $Variable = Resolve-Path "$DevelopmentPath\$DevelopmentFolderCore\$BinaryModule"
                $DevelopmentAssemblyFolder = Resolve-Path "$DevelopmentPath\$DevelopmentFolderCore"
            } else {
                $Variable = Resolve-Path "$DevelopmentPath\$DevelopmentFolderDefault\$BinaryModule"
                $DevelopmentAssemblyFolder = Resolve-Path "$DevelopmentPath\$DevelopmentFolderDefault"
            }
            $Variable
            Write-Warning "Development mode: Using binaries from $Variable"
        }
    }
)

if ($Development) {
    $Assembly = @(Get-ChildItem -Path "$($DevelopmentAssemblyFolder.Path)\*.dll" -ErrorAction SilentlyContinue -File | Where-Object {
            $_.Name -notin $BinaryModules -and $_.Name -notin $SkipAssemblyNames
        })
} else {
    $Assembly = @(
        if ($Framework -and $PSEdition -eq 'Core') {
            Get-ChildItem -Path $PSScriptRoot\Lib\$Framework\*.dll -ErrorAction SilentlyContinue | Where-Object {
                $_.Name -notin $BinaryModules -and $_.Name -notin $SkipAssemblyNames
            }
        }
        if ($FrameworkNet -and $PSEdition -ne 'Core') {
            Get-ChildItem -Path $PSScriptRoot\Lib\$FrameworkNet\*.dll -ErrorAction SilentlyContinue | Where-Object {
                $_.Name -notin $BinaryModules -and $_.Name -notin $SkipAssemblyNames
            }
        }
    )
}

$FoundErrors = @(
    if ($Development) {
        foreach ($BinaryModule in $BinaryDev) {
            try {
                Import-Module -Name $BinaryModule -Force -ErrorAction Stop
            } catch {
                Write-Warning "Failed to import module $($BinaryModule): $($_.Exception.Message)"
                $true
            }
        }
    } else {
        foreach ($BinaryModule in $BinaryModules) {
            try {
                if ($Framework -and $PSEdition -eq 'Core') {
                    Import-Module -Name "$PSScriptRoot\Lib\$Framework\$BinaryModule" -Force -ErrorAction Stop
                }
                if ($FrameworkNet -and $PSEdition -ne 'Core') {
                    Import-Module -Name "$PSScriptRoot\Lib\$FrameworkNet\$BinaryModule" -Force -ErrorAction Stop
                }
            } catch {
                Write-Warning "Failed to import module $($BinaryModule): $($_.Exception.Message)"
                $true
            }
        }
    }
    foreach ($Import in @($Assembly)) {
        try {
            Add-Type -Path $Import.FullName -ErrorAction Stop
        } catch [System.Reflection.ReflectionTypeLoadException] {
            Write-Warning "Processing $($Import.Name) Exception: $($_.Exception.Message)"
            $LoaderExceptions = $($_.Exception.LoaderExceptions) | Sort-Object -Unique
            foreach ($E in $LoaderExceptions) {
                Write-Warning "Processing $($Import.Name) LoaderExceptions: $($E.Message)"
            }
            $true
        } catch {
            Write-Warning "Processing $($Import.Name) Exception: $($_.Exception.Message)"
            $LoaderExceptions = @()
            if ($_.Exception -is [System.Reflection.ReflectionTypeLoadException]) {
                $LoaderExceptions = @($_.Exception.LoaderExceptions) | Sort-Object -Unique
            }
            foreach ($E in $LoaderExceptions) {
                Write-Warning "Processing $($Import.Name) LoaderExceptions: $($E.Message)"
            }
            $true
        }
    }
    # Dot source the files
    foreach ($Import in @($Classes + $Enums + $Private + $Public)) {
        try {
            . $Import.FullName
        } catch {
            Write-Error -Message "Failed to import functions from $($Import.FullName): $_"
            $true
        }
    }
)

if ($FoundErrors.Count -gt 0) {
    $ModuleName = (Get-ChildItem $PSScriptRoot\*.psd1).BaseName
    Write-Warning "Importing module $ModuleName failed. Fix errors before continuing."
    break
}

Export-ModuleMember -Function '*' -Alias '*' -Cmdlet '*'
