using System.Collections.Generic;
namespace Mailozaurr.NonDeliveryReports;

/// <summary>Provides subject patterns used to detect Non-Delivery Reports.</summary>
public interface INonDeliveryReportSubjectPatternProvider {
    /// <summary>Collection of patterns matched against message subjects.</summary>
    ICollection<string> SubjectPatterns { get; }
}

