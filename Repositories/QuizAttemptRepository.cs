using backend.Data;
using backend.DTOs;
using backend.Models;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation for quiz_attempts and attempt_answers.
/// Uses parameterized queries to prevent SQL injection.
/// </summary>
public class QuizAttemptRepository : IQuizAttemptRepository
{
    private readonly DatabaseHelper _db;

    public QuizAttemptRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<QuizAttempt> CreateAttemptAsync(QuizAttempt attempt)
    {
        const string sql = @"
            INSERT INTO quiz_attempts
                (user_id, quiz_id, score, total_questions, xp_earned, passed, started_at, completed_at)
            VALUES
                (@UserId, @QuizId, @Score, @TotalQuestions, @XpEarned, @Passed, @StartedAt, @CompletedAt);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@UserId",         attempt.UserId);
        cmd.Parameters.AddWithValue("@QuizId",         attempt.QuizId);
        cmd.Parameters.AddWithValue("@Score",          attempt.Score);
        cmd.Parameters.AddWithValue("@TotalQuestions", attempt.TotalQuestions);
        cmd.Parameters.AddWithValue("@XpEarned",       attempt.XpEarned);
        cmd.Parameters.AddWithValue("@Passed",         attempt.Passed);
        cmd.Parameters.AddWithValue("@StartedAt",      attempt.StartedAt);
        cmd.Parameters.AddWithValue("@CompletedAt",    (object?)attempt.CompletedAt ?? DBNull.Value);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        attempt.AttemptId = insertedId;
        return attempt;
    }

    /// <inheritdoc />
    public async Task<AttemptAnswer> CreateAnswerAsync(AttemptAnswer answer)
    {
        const string sql = @"
            INSERT INTO attempt_answers
                (attempt_id, question_id, selected_option, is_correct)
            VALUES
                (@AttemptId, @QuestionId, @SelectedOption, @IsCorrect);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@AttemptId",      answer.AttemptId);
        cmd.Parameters.AddWithValue("@QuestionId",     answer.QuestionId);
        cmd.Parameters.AddWithValue("@SelectedOption", answer.SelectedOption);
        cmd.Parameters.AddWithValue("@IsCorrect",      answer.IsCorrect);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        answer.AnswerId = insertedId;
        return answer;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AttemptAnswer>> CreateAnswersAsync(IEnumerable<AttemptAnswer> answers)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
            return answerList;

        const string sql = @"
            INSERT INTO attempt_answers
                (attempt_id, question_id, selected_option, is_correct)
            VALUES
                (@AttemptId, @QuestionId, @SelectedOption, @IsCorrect);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var answer in answerList)
            {
                await using var cmd = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
                cmd.Parameters.AddWithValue("@AttemptId",      answer.AttemptId);
                cmd.Parameters.AddWithValue("@QuestionId",     answer.QuestionId);
                cmd.Parameters.AddWithValue("@SelectedOption", answer.SelectedOption);
                cmd.Parameters.AddWithValue("@IsCorrect",      answer.IsCorrect);

                var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                answer.AnswerId = insertedId;
            }

            await transaction.CommitAsync();
            return answerList;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasExistingAttemptAsync(int userId, int quizId)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM quiz_attempts
            WHERE user_id = @UserId
              AND quiz_id  = @QuizId
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@QuizId", quizId);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    /// <inheritdoc />
    public async Task<QuizAttempt> SubmitAttemptTransactionalAsync(
        QuizAttempt attempt,
        IEnumerable<AttemptAnswer> answers,
        int xpToAward)
    {
        var answerList = answers.ToList();

        const string insertAttemptSql = @"
            INSERT INTO quiz_attempts
                (user_id, quiz_id, score, total_questions, xp_earned, passed, started_at, completed_at)
            VALUES
                (@UserId, @QuizId, @Score, @TotalQuestions, @XpEarned, @Passed, @StartedAt, @CompletedAt);
            SELECT LAST_INSERT_ID();";

        const string insertAnswerSql = @"
            INSERT INTO attempt_answers
                (attempt_id, question_id, selected_option, is_correct)
            VALUES
                (@AttemptId, @QuestionId, @SelectedOption, @IsCorrect);
            SELECT LAST_INSERT_ID();";

        const string awardXpSql = @"
            UPDATE Users
            SET XpTotal = XpTotal + @XpToAward
            WHERE Id = @UserId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Insert attempt
            await using var attemptCmd = new MySqlCommand(insertAttemptSql, connection, (MySqlTransaction)transaction);
            attemptCmd.Parameters.AddWithValue("@UserId",         attempt.UserId);
            attemptCmd.Parameters.AddWithValue("@QuizId",         attempt.QuizId);
            attemptCmd.Parameters.AddWithValue("@Score",          attempt.Score);
            attemptCmd.Parameters.AddWithValue("@TotalQuestions", attempt.TotalQuestions);
            attemptCmd.Parameters.AddWithValue("@XpEarned",       attempt.XpEarned);
            attemptCmd.Parameters.AddWithValue("@Passed",         attempt.Passed);
            attemptCmd.Parameters.AddWithValue("@StartedAt",      attempt.StartedAt);
            attemptCmd.Parameters.AddWithValue("@CompletedAt",    (object?)attempt.CompletedAt ?? DBNull.Value);

            attempt.AttemptId = Convert.ToInt32(await attemptCmd.ExecuteScalarAsync());

            // 2. Insert answers
            foreach (var answer in answerList)
            {
                answer.AttemptId = attempt.AttemptId;
                await using var answerCmd = new MySqlCommand(insertAnswerSql, connection, (MySqlTransaction)transaction);
                answerCmd.Parameters.AddWithValue("@AttemptId",      answer.AttemptId);
                answerCmd.Parameters.AddWithValue("@QuestionId",     answer.QuestionId);
                answerCmd.Parameters.AddWithValue("@SelectedOption", answer.SelectedOption);
                answerCmd.Parameters.AddWithValue("@IsCorrect",      answer.IsCorrect);

                answer.AnswerId = Convert.ToInt32(await answerCmd.ExecuteScalarAsync());
            }

            // 3. Award XP to the user (only when there is XP to award)
            if (xpToAward > 0)
            {
                await using var xpCmd = new MySqlCommand(awardXpSql, connection, (MySqlTransaction)transaction);
                xpCmd.Parameters.AddWithValue("@XpToAward", xpToAward);
                xpCmd.Parameters.AddWithValue("@UserId",    attempt.UserId);
                await xpCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return attempt;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuizAttempt>> GetAllAsync()
    {
        const string sql = @"
            SELECT attempt_id, user_id, quiz_id, score, total_questions, xp_earned, passed, started_at, completed_at
            FROM quiz_attempts
            ORDER BY started_at DESC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        var attempts = new List<QuizAttempt>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            attempts.Add(new QuizAttempt
            {
                AttemptId = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                QuizId = reader.GetInt32(2),
                Score = reader.GetInt32(3),
                TotalQuestions = reader.GetInt32(4),
                XpEarned = reader.GetInt32(5),
                Passed = reader.GetBoolean(6),
                StartedAt = reader.GetDateTime(7),
                CompletedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            });
        }

        return attempts;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<QuizAttempt> Attempts, int TotalCount)> GetAttemptsForUserAsync(int userId, int pageNumber, int pageSize)
    {
        const string countSql = @"
            SELECT COUNT(1)
            FROM quiz_attempts
            WHERE user_id = @UserId;";

        const string selectSql = @"
            SELECT attempt_id, user_id, quiz_id, score, total_questions, xp_earned, passed, started_at, completed_at
            FROM quiz_attempts
            WHERE user_id = @UserId
            ORDER BY completed_at DESC, started_at DESC
            LIMIT @PageSize OFFSET @Offset;";

        await using var connection = await _db.OpenConnectionAsync();

        // Get total count
        await using var countCmd = new MySqlCommand(countSql, connection);
        countCmd.Parameters.AddWithValue("@UserId", userId);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        // Get paginated attempts
        await using var selectCmd = new MySqlCommand(selectSql, connection);
        selectCmd.Parameters.AddWithValue("@UserId", userId);
        selectCmd.Parameters.AddWithValue("@PageSize", pageSize);
        selectCmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);

        var attempts = new List<QuizAttempt>();
        await using var reader = (MySqlDataReader)await selectCmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            attempts.Add(new QuizAttempt
            {
                AttemptId = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                QuizId = reader.GetInt32(2),
                Score = reader.GetInt32(3),
                TotalQuestions = reader.GetInt32(4),
                XpEarned = reader.GetInt32(5),
                Passed = reader.GetBoolean(6),
                StartedAt = reader.GetDateTime(7),
                CompletedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            });
        }

        return (attempts, totalCount);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuizAttemptHistoryItemDto>> GetAttemptHistoryByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT
                qa.attempt_id,
                qa.quiz_id,
                q.title                                        AS quiz_title,
                a.name                                         AS algorithm_name,
                qa.score,
                qa.total_questions,
                CASE
                    WHEN qa.total_questions > 0 THEN (qa.score * 100.0 / qa.total_questions)
                    ELSE 0
                END                                            AS score_percent,
                qa.xp_earned,
                qa.passed,
                qa.completed_at
            FROM quiz_attempts qa
            INNER JOIN quizzes    q ON q.quiz_id      = qa.quiz_id
            INNER JOIN algorithms a ON a.algorithm_id = q.algorithm_id
            WHERE qa.user_id = @UserId
            ORDER BY qa.completed_at DESC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var history = new List<QuizAttemptHistoryItemDto>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            history.Add(MapAttemptHistoryItem(reader));
        }

        return history;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AlgorithmCoverageItemDto>> GetAlgorithmCoverageByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT
                a.algorithm_id                                                       AS algorithm_id,
                a.name                                                               AS algorithm_name,
                a.category                                                           AS category,
                COUNT(qa.attempt_id)                                                AS total_attempts,
                SUM(CASE WHEN qa.passed = 1 THEN 1 ELSE 0 END)                     AS passed_attempts,
                MAX(CASE
                        WHEN qa.total_questions > 0 THEN (qa.score * 100.0 / qa.total_questions)
                        ELSE NULL
                    END)                                                            AS best_score_percent,
                MAX(CASE WHEN qa.passed = 1 THEN 1 ELSE 0 END)                     AS has_passed_quiz
            FROM algorithms a
            LEFT JOIN quizzes       q  ON q.algorithm_id = a.algorithm_id AND q.is_active = 1
            LEFT JOIN quiz_attempts qa ON qa.quiz_id      = q.quiz_id    AND qa.user_id  = @UserId
            GROUP BY a.algorithm_id, a.name, a.category
            ORDER BY a.name ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var coverage = new List<AlgorithmCoverageItemDto>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            coverage.Add(MapAlgorithmCoverageItem(reader));
        }

        return coverage;
    }

    /// <inheritdoc />
    public async Task<PerformanceSummaryDto> GetPerformanceSummaryByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT
                COUNT(*)                                                AS total_attempts,
                SUM(CASE WHEN passed = 1 THEN 1 ELSE 0 END)            AS total_passed,
                AVG(CASE
                        WHEN total_questions > 0 THEN (score * 100.0 / total_questions)
                        ELSE NULL
                    END)                                                AS average_score,
                SUM(xp_earned)                                          AS total_xp_from_quizzes
            FROM quiz_attempts
            WHERE user_id = @UserId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        var totalAttempts = Convert.ToInt32(reader["total_attempts"]);
        var totalPassed   = reader["total_passed"]           == DBNull.Value ? 0   : Convert.ToInt32(reader["total_passed"]);
        var averageScore  = reader["average_score"]          == DBNull.Value ? 0.0 : Convert.ToDouble(reader["average_score"]);
        var totalXp       = reader["total_xp_from_quizzes"]  == DBNull.Value ? 0   : Convert.ToInt32(reader["total_xp_from_quizzes"]);

        return new PerformanceSummaryDto
        {
            TotalAttempts      = totalAttempts,
            TotalPassed        = totalPassed,
            PassRate           = totalAttempts > 0 ? Math.Round((double)totalPassed / totalAttempts * 100, 2) : 0.0,
            AverageScore       = Math.Round(averageScore, 2),
            TotalXpFromQuizzes = totalXp
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ActivityItemDto>> GetRecentActivityAsync(int userId, int limit)
    {
        await using var connection = await _db.OpenConnectionAsync();
        var items = new List<ActivityItemDto>();

        const string quizSql = @"
            SELECT
                q.title         AS title,
                qa.xp_earned    AS xp_earned,
                qa.completed_at AS created_at
            FROM quiz_attempts qa
            JOIN quizzes q ON q.quiz_id = qa.quiz_id
            WHERE qa.user_id = @UserId
              AND qa.completed_at IS NOT NULL
            ORDER BY qa.completed_at DESC
            LIMIT @Limit;";

        await using (var quizCmd = new MySqlCommand(quizSql, connection))
        {
            quizCmd.Parameters.AddWithValue("@UserId", userId);
            quizCmd.Parameters.AddWithValue("@Limit", limit);

            await using var quizReader = (MySqlDataReader)await quizCmd.ExecuteReaderAsync();
            while (await quizReader.ReadAsync())
            {
                items.Add(new ActivityItemDto
                {
                    Type = "quiz",
                    Title = (string)quizReader["title"],
                    XpEarned = Convert.ToInt32(quizReader["xp_earned"]),
                    CreatedAt = Convert.ToDateTime(quizReader["created_at"]),
                    Metadata = null
                });
            }
        }

        const string badgeSql = @"
            SELECT
                b.badge_name  AS title,
                ub.awarded_at AS created_at
            FROM user_badges ub
            JOIN badges b ON b.badge_id = ub.badge_id
            WHERE ub.user_id = @UserId
            ORDER BY ub.awarded_at DESC
            LIMIT @Limit;";

        await using (var badgeCmd = new MySqlCommand(badgeSql, connection))
        {
            badgeCmd.Parameters.AddWithValue("@UserId", userId);
            badgeCmd.Parameters.AddWithValue("@Limit", limit);

            await using var badgeReader = (MySqlDataReader)await badgeCmd.ExecuteReaderAsync();
            while (await badgeReader.ReadAsync())
            {
                items.Add(new ActivityItemDto
                {
                    Type = "badge",
                    Title = (string)badgeReader["title"],
                    XpEarned = 0,
                    CreatedAt = Convert.ToDateTime(badgeReader["created_at"]),
                    Metadata = null
                });
            }
        }

        return items
            .OrderByDescending(item => item.CreatedAt)
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ActivityHeatmapDto>> GetDailyActivityAsync(int userId)
    {
        const string sql = @"
            SELECT
                DATE(completed_at) AS date,
                COUNT(*)           AS count
            FROM quiz_attempts
            WHERE user_id      = @UserId
              AND completed_at IS NOT NULL
            GROUP BY DATE(completed_at)
            ORDER BY date ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var results = new List<ActivityHeatmapDto>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new ActivityHeatmapDto
            {
                Date  = Convert.ToDateTime(reader["date"]),
                Count = Convert.ToInt32(reader["count"])
            });
        }

        return results;
    }

    // -------------------------------------------------------------------------
    // Private mapping helpers
    // -------------------------------------------------------------------------

    private static QuizAttemptHistoryItemDto MapAttemptHistoryItem(MySqlDataReader reader)
    {
        return new QuizAttemptHistoryItemDto
        {
            AttemptId     = Convert.ToInt32(reader["attempt_id"]),
            QuizId        = Convert.ToInt32(reader["quiz_id"]),
            QuizTitle     = (string)reader["quiz_title"],
            AlgorithmName = (string)reader["algorithm_name"],
            Score         = Convert.ToInt32(reader["score"]),
            TotalQuestions = Convert.ToInt32(reader["total_questions"]),
            ScorePercent  = reader["score_percent"] == DBNull.Value ? 0.0 : Math.Round(Convert.ToDouble(reader["score_percent"]), 2),
            XpEarned      = Convert.ToInt32(reader["xp_earned"]),
            Passed        = Convert.ToBoolean(reader["passed"]),
            CompletedAt   = reader["completed_at"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(reader["completed_at"])
        };
    }

    private static AlgorithmCoverageItemDto MapAlgorithmCoverageItem(MySqlDataReader reader)
    {
        return new AlgorithmCoverageItemDto
        {
            AlgorithmId      = Convert.ToInt32(reader["algorithm_id"]),
            AlgorithmName    = (string)reader["algorithm_name"],
            Category         = (string)reader["category"],
            TotalAttempts    = Convert.ToInt32(reader["total_attempts"]),
            PassedAttempts   = reader["passed_attempts"]    == DBNull.Value ? 0   : Convert.ToInt32(reader["passed_attempts"]),
            BestScorePercent = reader["best_score_percent"] == DBNull.Value ? 0.0 : Math.Round(Convert.ToDouble(reader["best_score_percent"]), 2),
            HasPassedQuiz    = reader["has_passed_quiz"]    == DBNull.Value ? false : Convert.ToBoolean(reader["has_passed_quiz"])
        };
    }
}
