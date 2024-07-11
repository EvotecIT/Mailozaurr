namespace Mailozaurr.PowerShell;

/// <summary>
/// Eml conversion result
/// </summary>
public class EmlConversionResult {
    /// <summary>
    /// Eml file path to file that was converted
    /// </summary>
    public string EmlFile { get; set; }
    /// <summary>
    /// Msg file path to file that was created
    /// </summary>
    public string MsgFile { get; set; }
    /// <summary>
    /// Status of the conversion
    /// </summary>
    public bool Status { get; set; }
    /// <summary>
    /// Error message if conversion failed
    /// </summary>
    public string Error { get; set; }
}