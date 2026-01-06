namespace Mailozaurr;

/// <summary>
/// Result information returned when converting an EML file to MSG.
/// </summary>
/// <remarks>
/// Used by <see cref="EmailMessage.ConvertEmlToMsg(string[], string, bool)"/> to report
/// the path of the generated message and whether the operation succeeded.
/// </remarks>
public class EmlConversionResult {
    /// <summary>
    /// Eml file path to file that was converted
    /// </summary>
    public string EmlFile { get; set; } = string.Empty;
    /// <summary>
    /// Msg file path to file that was created
    /// </summary>
    public string MsgFile { get; set; } = string.Empty;
    /// <summary>
    /// Status of the conversion
    /// </summary>
    public bool Status { get; set; }
    /// <summary>
    /// Error message if conversion failed
    /// </summary>
    public string? Error { get; set; }
}