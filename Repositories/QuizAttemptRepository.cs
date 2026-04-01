using backend.Data;
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
}
