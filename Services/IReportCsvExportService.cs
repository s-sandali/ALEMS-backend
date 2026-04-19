using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Creates CSV exports for report datasets.
/// </summary>
public interface IReportCsvExportService
{
    /// <summary>
    /// Serializes the aggregated admin report data into a structured CSV stream.
    /// </summary>
    /// <param name="bundle">The aggregated report payload.</param>
    /// <param name="startDate">Inclusive report start date.</param>
    /// <param name="endDate">Inclusive report end date.</param>
    /// <returns>A stream positioned at 0 and ready for file download.</returns>
    Stream CreateAdminReportCsv(AdminReportBundleDto bundle, DateTime startDate, DateTime endDate);
}
