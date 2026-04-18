using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Creates PDF exports for report datasets.
/// </summary>
public interface IReportPdfExportService
{
    /// <summary>
    /// Formats the aggregated admin report data into a structured PDF document.
    /// </summary>
    /// <param name="bundle">The aggregated report payload.</param>
    /// <param name="startDate">Inclusive report start date.</param>
    /// <param name="endDate">Inclusive report end date.</param>
    /// <returns>PDF document bytes ready for file download.</returns>
    byte[] CreateAdminReportPdf(AdminReportBundleDto bundle, DateTime startDate, DateTime endDate);
}
