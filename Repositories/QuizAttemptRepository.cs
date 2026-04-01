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
}
