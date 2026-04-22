using backend.Data;
using backend.DTOs;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation for report data aggregations.
/// Executes parameterized queries to prevent SQL injection and support flexible date range filters.
/// </summary>
public class ReportRepository : IReportRepository
{
    private readonly DatabaseHelper _db;

    public ReportRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PerStudentReportDto>> GetPerStudentReportAsync(DateTime startDate, DateTime endDate)
    {
        // Issue #202: quiz_attempts no longer exposes created_at; use the aligned timestamps instead.
        const string sql = @"
            SELECT 
                u.id AS student_id,
                u.email AS student_name,
                COUNT(qa.attempt_id) AS total_attempts,
                AVG(qa.score) AS avg_score,
                MAX(qa.score) AS best_score,
                COALESCE(SUM(qa.xp_earned), 0) AS total_xp,
                COUNT(DISTINCT qa.quiz_id) AS algorithms_attempted
            FROM quiz_attempts qa
            JOIN Users u ON qa.user_id = u.id
            WHERE COALESCE(qa.completed_at, qa.submitted_at) BETWEEN @StartDate AND @EndDate
            GROUP BY u.id, u.email
            ORDER BY u.email;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@StartDate", startDate);
        cmd.Parameters.AddWithValue("@EndDate", endDate);

        var results = new List<PerStudentReportDto>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new PerStudentReportDto
            {
                StudentId = reader.GetInt32(0),
                StudentName = reader.GetString(1),
                TotalAttempts = reader.GetInt32(2),
                AverageScore = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)),
                BestScore = reader.GetInt32(4),
                TotalXp = reader.GetInt32(5),
                AlgorithmsAttempted = reader.GetInt32(6)
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PerAlgorithmReportDto>> GetPerAlgorithmReportAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT 
                a.category AS algorithm_type,
                COUNT(qa.attempt_id) AS attempt_count,
                AVG(qa.score) AS avg_score,
                SUM(CASE WHEN qa.score >= 50 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS pass_rate
            FROM quiz_attempts qa
            JOIN quizzes q ON qa.quiz_id = q.quiz_id
            JOIN algorithms a ON q.algorithm_id = a.algorithm_id
            WHERE COALESCE(qa.completed_at, qa.submitted_at) BETWEEN @StartDate AND @EndDate
            GROUP BY a.category
            ORDER BY a.category;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@StartDate", startDate);
        cmd.Parameters.AddWithValue("@EndDate", endDate);

        var results = new List<PerAlgorithmReportDto>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new PerAlgorithmReportDto
            {
                AlgorithmType = reader.GetString(0),
                AttemptCount = reader.GetInt32(1),
                AverageScore = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                PassRate = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3))
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PerQuizReportDto>> GetPerQuizReportAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT 
                q.title,
                COUNT(qa.attempt_id) AS attempt_count,
                AVG(qa.score) AS avg_score,
                MAX(qa.score) AS highest_score,
                MIN(qa.score) AS lowest_score
            FROM quiz_attempts qa
            JOIN quizzes q ON qa.quiz_id = q.quiz_id
            WHERE COALESCE(qa.completed_at, qa.submitted_at) BETWEEN @StartDate AND @EndDate
            GROUP BY q.quiz_id, q.title
            ORDER BY q.title;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@StartDate", startDate);
        cmd.Parameters.AddWithValue("@EndDate", endDate);

        var results = new List<PerQuizReportDto>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new PerQuizReportDto
            {
                Title = reader.GetString(0),
                AttemptCount = reader.GetInt32(1),
                AverageScore = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                HighestScore = reader.GetInt32(3),
                LowestScore = reader.GetInt32(4)
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<SummaryStatisticsDto> GetSummaryStatisticsAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT 
                COUNT(*) AS total_attempts,
                COUNT(DISTINCT user_id) AS total_students,
                AVG(score) AS avg_score,
                COALESCE(SUM(xp_earned), 0) AS total_xp
            FROM quiz_attempts
            WHERE COALESCE(completed_at, submitted_at) BETWEEN @StartDate AND @EndDate;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@StartDate", startDate);
        cmd.Parameters.AddWithValue("@EndDate", endDate);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SummaryStatisticsDto
            {
                TotalAttempts = reader.GetInt32(0),
                TotalStudents = reader.GetInt32(1),
                AverageScore = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                TotalXp = reader.GetInt32(3)
            };
        }

        // Fallback to zero values if query returns no rows
        return new SummaryStatisticsDto
        {
            TotalAttempts = 0,
            TotalStudents = 0,
            AverageScore = 0,
            TotalXp = 0
        };
    }
}
