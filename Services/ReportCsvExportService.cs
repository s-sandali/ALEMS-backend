using System.Globalization;
using System.Text;
using backend.DTOs;

namespace backend.Services;

/// <summary>
/// CSV serialization implementation for admin reports.
/// </summary>
public class ReportCsvExportService : IReportCsvExportService
{
    /// <inheritdoc />
    public Stream CreateAdminReportCsv(AdminReportBundleDto bundle, DateTime startDate, DateTime endDate)
    {
        var builder = new StringBuilder();

        AppendRow(builder, "section", "start_date", "end_date");
        AppendRow(builder, "report_metadata", startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        builder.AppendLine();

        AppendRow(builder, "section", "total_attempts", "total_students", "avg_score", "total_xp");
        AppendRow(builder,
            "summary_statistics",
            bundle.Summary.TotalAttempts.ToString(CultureInfo.InvariantCulture),
            bundle.Summary.TotalStudents.ToString(CultureInfo.InvariantCulture),
            bundle.Summary.AverageScore.ToString("F2", CultureInfo.InvariantCulture),
            bundle.Summary.TotalXp.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine();

        AppendRow(builder, "section", "student_id", "student_name", "total_attempts", "avg_score", "best_score", "total_xp", "algorithms_attempted");
        foreach (var item in bundle.PerStudent)
        {
            AppendRow(builder,
                "per_student",
                item.StudentId.ToString(CultureInfo.InvariantCulture),
                item.StudentName,
                item.TotalAttempts.ToString(CultureInfo.InvariantCulture),
                item.AverageScore.ToString("F2", CultureInfo.InvariantCulture),
                item.BestScore.ToString(CultureInfo.InvariantCulture),
                item.TotalXp.ToString(CultureInfo.InvariantCulture),
                item.AlgorithmsAttempted.ToString(CultureInfo.InvariantCulture));
        }
        builder.AppendLine();

        AppendRow(builder, "section", "algorithm_type", "attempt_count", "avg_score", "pass_rate");
        foreach (var item in bundle.PerAlgorithm)
        {
            AppendRow(builder,
                "per_algorithm",
                item.AlgorithmType,
                item.AttemptCount.ToString(CultureInfo.InvariantCulture),
                item.AverageScore.ToString("F2", CultureInfo.InvariantCulture),
                item.PassRate.ToString("F2", CultureInfo.InvariantCulture));
        }
        builder.AppendLine();

        AppendRow(builder, "section", "title", "attempt_count", "avg_score", "highest_score", "lowest_score");
        foreach (var item in bundle.PerQuiz)
        {
            AppendRow(builder,
                "per_quiz",
                item.Title,
                item.AttemptCount.ToString(CultureInfo.InvariantCulture),
                item.AverageScore.ToString("F2", CultureInfo.InvariantCulture),
                item.HighestScore.ToString(CultureInfo.InvariantCulture),
                item.LowestScore.ToString(CultureInfo.InvariantCulture));
        }

        var csvBytes = Encoding.UTF8.GetBytes(builder.ToString());
        return new MemoryStream(csvBytes);
    }

    private static void AppendRow(StringBuilder builder, params string[] values)
    {
        var escaped = values.Select(EscapeCsv);
        builder.AppendLine(string.Join(",", escaped));
    }

    private static string EscapeCsv(string value)
    {
        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var normalized = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{normalized}\"" : normalized;
    }
}
