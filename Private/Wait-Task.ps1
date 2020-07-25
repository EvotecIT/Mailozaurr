function Wait-Task {
    # await replacement
    param (
        [Parameter(ValueFromPipeline = $true, Mandatory = $true)] $Task
    )
    # https://stackoverflow.com/questions/51218257/await-async-c-sharp-method-from-powershell
    process {
        while (-not $Task.AsyncWaitHandle.WaitOne(200)) { }
        $Task.GetAwaiter().GetResult()
    }
}