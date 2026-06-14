using System.Collections.Generic;
using System.Management.Automation;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Rewrites local HTML image paths to cid references and returns the discovered files.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlLocalImagePath")]
[OutputType(typeof(HtmlLocalImagePathResult))]
public sealed class CmdletConvertFromHtmlLocalImagePath : PSCmdlet {
    /// <summary>HTML content that may contain local image paths.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNull]
    public string? Html { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        var result = HtmlUtils.ExtractLocalImagePaths(Html!);
        WriteObject(new HtmlLocalImagePathResult(result.Html, result.Paths));
    }
}

/// <summary>
/// Result returned by <see cref="CmdletConvertFromHtmlLocalImagePath"/>.
/// </summary>
public sealed class HtmlLocalImagePathResult {
    internal HtmlLocalImagePathResult(string html, IReadOnlyList<string> paths) {
        Html = html;
        Paths = paths;
    }

    /// <summary>HTML with local image sources replaced by cid references.</summary>
    public string Html { get; }

    /// <summary>Local file paths discovered in the HTML.</summary>
    public IReadOnlyList<string> Paths { get; }
}
