# Get public and private function definition files.
$Public = @( Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 -ErrorAction SilentlyContinue -Recurse )
$Private = @( Get-ChildItem -Path $PSScriptRoot\Private\*.ps1 -ErrorAction SilentlyContinue -Recurse )
$Classes = @( Get-ChildItem -Path $PSScriptRoot\Classes\*.ps1 -ErrorAction SilentlyContinue -Recurse )
$Enums = @( Get-ChildItem -Path $PSScriptRoot\Enums\*.ps1 -ErrorAction SilentlyContinue -Recurse )
# Get all assemblies
$AssemblyFolders = Get-ChildItem -Path $PSScriptRoot\Lib -Directory -ErrorAction SilentlyContinue

# Lets find which libraries we need to load
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
} else {
    Write-Error -Message 'No assemblies found'
}
if ($PSEdition -eq 'Core') {
    $LibFolder = $Framework
} else {
    $LibFolder = $FrameworkNet
}

$Assembly = @(
    if ($Framework -and $PSEdition -eq 'Core') {
        Get-ChildItem -Path $PSScriptRoot\Lib\$Framework\*.dll -ErrorAction SilentlyContinue -Recurse
    }
    if ($FrameworkNet -and $PSEdition -ne 'Core') {
        Get-ChildItem -Path $PSScriptRoot\Lib\$FrameworkNet\*.dll -ErrorAction SilentlyContinue -Recurse
    }
    # if ($AssemblyFolders.BaseName -contains 'Standard') {
    #     @( Get-ChildItem -Path $PSScriptRoot\Lib\Standard\*.dll -ErrorAction SilentlyContinue -Recurse)
    # }
    # if ($PSEdition -eq 'Core') {
    #     @( Get-ChildItem -Path $PSScriptRoot\Lib\Core\*.dll -ErrorAction SilentlyContinue -Recurse )
    # } else {
    #     @( Get-ChildItem -Path $PSScriptRoot\Lib\Default\*.dll -ErrorAction SilentlyContinue -Recurse )
    # }
)

# This is special way of importing DLL if multiple frameworks are in use
$FoundErrors = @(
    # We load the DLL that does OnImportRemove if we have special module that requires special treatment for binary modules

    # Get library name, from the PSM1 file name
    $LibraryName = $myInvocation.MyCommand.Name.Replace(".psm1", "")
    $Library = "$LibraryName.dll"
    $Class = "$LibraryName.Initialize"

    try {
        $ImportModule = Get-Command -Name Import-Module -Module Microsoft.PowerShell.Core

        if (-not ($Class -as [type])) {
            & $ImportModule ([IO.Path]::Combine($PSScriptRoot, 'Lib', $LibFolder, $Library)) -ErrorAction Stop
        } else {
            $Type = "$Class" -as [Type]
            & $importModule -Force -Assembly ($Type.Assembly)
        }
    } catch {
        Write-Warning -Message "Importing module $Library failed. Fix errors before continuing. Error: $($_.Exception.Message)"
        $true
    }

    Foreach ($Import in @($Assembly)) {
        try {
            Add-Type -Path $Import.Fullname -ErrorAction Stop
        } catch [System.Reflection.ReflectionTypeLoadException] {
            Write-Warning "Processing $($Import.Name) Exception: $($_.Exception.Message)"
            $LoaderExceptions = $($_.Exception.LoaderExceptions) | Sort-Object -Unique
            foreach ($E in $LoaderExceptions) {
                Write-Warning "Processing $($Import.Name) LoaderExceptions: $($E.Message)"
            }
            $true
        } catch {
            Write-Warning "Processing $($Import.Name) Exception: $($_.Exception.Message)"
            $LoaderExceptions = $($_.Exception.LoaderExceptions) | Sort-Object -Unique
            foreach ($E in $LoaderExceptions) {
                Write-Warning "Processing $($Import.Name) LoaderExceptions: $($E.Message)"
            }
            $true
        }
    }

    #Dot source the files
    Foreach ($Import in @($Private + $Classes + $Enums + $Public)) {
        Try {
            . $Import.Fullname
        } Catch {
            Write-Warning -Message "Failed to import functions from $($import.Fullname).Error: $($_.Exception.Message)"
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