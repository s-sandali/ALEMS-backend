namespace backend.DTOs;

/// <summary>
/// Aggregated report payload used by the admin reports export endpoint.
/// </summary>
public class AdminReportBundleDto
{
    public SummaryStatisticsDto Summary { get; set; } = new();

    public IEnumerable<PerStudentReportDto> PerStudent { get; set; } = Array.Empty<PerStudentReportDto>();

    public IEnumerable<PerAlgorithmReportDto> PerAlgorithm { get; set; } = Array.Empty<PerAlgorithmReportDto>();

    public IEnumerable<PerQuizReportDto> PerQuiz { get; set; } = Array.Empty<PerQuizReportDto>();
}
