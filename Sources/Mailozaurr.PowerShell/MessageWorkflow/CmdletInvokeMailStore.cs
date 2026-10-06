using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Runs a script block with an automatically closed local mail-store scope.</para>
/// <para type="description">Passes one store scope to the script block. Repeated Get-MailMessage calls reuse its reader. The store closes on completion, cancellation or a terminating error. Returned message views remain usable while their source file is unchanged.</para>
/// <example><summary>Read several groups from one open store</summary><code>Invoke-MailStore './Archive.pst' { param($store) $store | Get-MailMessage -Folder Inbox -First 10 }</code></example>
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "MailStore")]
[OutputType(typeof(object))]
public sealed class CmdletInvokeMailStore : PSCmdlet {
    /// <summary>Local archive file.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;
    /// <summary>Script block accepting the store as its first argument.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNull]
    public ScriptBlock? ScriptBlock { get; set; }
    /// <summary>Invokes the script block and releases the reader.</summary>
    protected override void ProcessRecord() {
        using var store = EmailMessageStore.Open(GetUnresolvedProviderPathFromPSPath(Path));
        foreach (PSObject value in ScriptBlock!.Invoke(store)) WriteObject(value);
    }
}
