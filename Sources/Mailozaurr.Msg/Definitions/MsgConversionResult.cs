namespace Mailozaurr;

/// <summary>
/// Result information returned when converting a MSG file to EML.
/// </summary>
/// <remarks>
/// Used by <see cref="EmailMessage.ConvertMsgToEml(string[], string, bool)"/> to expose
/// the source and target paths along with status information.
/// </remarks>
public class MsgConversionResult {
    /// <summary>
    /// Msg file path to file that was converted
    /// </summary>
    public string MsgFile { get; set; } = string.Empty;
    /// <summary>
    /// Eml file path to file that was created
    /// </summary>
    public string EmlFile { get; set; } = string.Empty;
    /// <summary>
    /// Status of the conversion
    /// </summary>
    public bool Status { get; set; }
    /// <summary>
    /// Error message if conversion failed
    /// </summary>
    public string? Error { get; set; }
}
