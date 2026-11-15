function Add-MailozaurrTestType {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $TypeDefinition,
        [Parameter(Mandatory)][string[]] $ReferencedAssemblies,
        [string[]] $CompilerOptions = @('/nowarn:1701,1702')
    )

    if ($PSEdition -ne 'Core') {
        $binaryDirectory = Split-Path -Parent ([Mailozaurr.Smtp].Assembly.Location)
        $references = @($ReferencedAssemblies) + @(
            Join-Path $binaryDirectory 'Microsoft.Bcl.AsyncInterfaces.dll'
            Join-Path $binaryDirectory 'System.Threading.Tasks.Extensions.dll'
        )
        $parameters = [CodeDom.Compiler.CompilerParameters]::new()
        $parameters.GenerateInMemory = $true
        $parameters.CompilerOptions = $CompilerOptions -join ' '
        $parameters.ReferencedAssemblies.AddRange([string[]](@('System.dll', 'System.Core.dll') + @($references | Select-Object -Unique)))
        Add-Type -TypeDefinition $TypeDefinition -CompilerParameters $parameters -ErrorAction Stop
        return
    }

    # A fake must implement the same runtime types as the cmdlet under test.
    # Add-Type's default load context otherwise creates a second provider identity.
    $assemblyPath = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N') + '.dll')
    try {
        $references = @($ReferencedAssemblies) + @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' -File | ForEach-Object { $_.FullName })
        Add-Type -TypeDefinition $TypeDefinition -ReferencedAssemblies ($references | Select-Object -Unique) -CompilerOptions $CompilerOptions -OutputAssembly $assemblyPath -ErrorAction Stop
        $context = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([Mailozaurr.Smtp].Assembly)
        $stream = [IO.MemoryStream]::new([IO.File]::ReadAllBytes($assemblyPath))
        try {
            $assembly = $context.LoadFromStream($stream)
        } finally {
            $stream.Dispose()
        }

        $accelerators = [psobject].Assembly.GetType('System.Management.Automation.TypeAccelerators')
        foreach ($type in $assembly.GetExportedTypes()) {
            $accelerators::Add($type.FullName, $type)
        }
    } finally {
        if (Test-Path -LiteralPath $assemblyPath) {
            Remove-Item -LiteralPath $assemblyPath -Force
        }
    }
}
