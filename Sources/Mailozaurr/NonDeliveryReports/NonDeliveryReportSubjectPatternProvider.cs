namespace Mailozaurr.NonDeliveryReports;

/// <summary>Provides access to the current <see cref="INonDeliveryReportSubjectPatternProvider"/> instance.</summary>
public static class NonDeliveryReportSubjectPatternProvider {
    /// <summary>Gets or sets the provider supplying subject patterns for Non-Delivery Report detection.</summary>
    public static INonDeliveryReportSubjectPatternProvider Current { get; set; } = new DefaultNonDeliveryReportSubjectPatternProvider();
}

