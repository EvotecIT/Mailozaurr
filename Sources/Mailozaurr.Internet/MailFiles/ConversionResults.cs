namespace Mailozaurr;

/// <summary>Result information returned when converting an EML file to MSG.</summary>
public class EmlConversionResult {
    /// <summary>EML source file path.</summary>
    public string EmlFile { get; set; } = string.Empty;
    /// <summary>MSG target file path.</summary>
    public string MsgFile { get; set; } = string.Empty;
    /// <summary>Whether conversion succeeded.</summary>
    public bool Status { get; set; }
    /// <summary>Error message when conversion failed.</summary>
    public string? Error { get; set; }
}

/// <summary>Result information returned when converting a MSG file to EML.</summary>
public class MsgConversionResult {
    /// <summary>MSG source file path.</summary>
    public string MsgFile { get; set; } = string.Empty;
    /// <summary>EML target file path.</summary>
    public string EmlFile { get; set; } = string.Empty;
    /// <summary>Whether conversion succeeded.</summary>
    public bool Status { get; set; }
    /// <summary>Error message when conversion failed.</summary>
    public string? Error { get; set; }
}
