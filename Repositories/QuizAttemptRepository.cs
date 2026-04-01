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
    private const string InsertAttemptSql = @"
        INSERT INTO quiz_attempts
            (user_id, quiz_id, score, total_questions, xp_earned, passed, started_at, completed_at)
        VALUES
            (@UserId, @QuizId, @Score, @TotalQuestions, @XpEarned, @Passed, @StartedAt, @CompletedAt);
        SELECT LAST_INSERT_ID();";

    private const string InsertAnswerSql = @"
        INSERT INTO attempt_answers
            (attempt_id, question_id, selected_option, is_correct)
        VALUES
            (@AttemptId, @QuestionId, @SelectedOption, @IsCorrect);
        SELECT LAST_INSERT_ID();";

    private const string AddUserXpSql = @"
        UPDATE Users
        SET XpTotal = XpTotal + @XpEarned
        WHERE Id = @UserId;";

    public QuizAttemptRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<QuizAttempt> CreateAttemptAsync(QuizAttempt attempt)
    {
        await using var connection = await _db.OpenConnectionAsync();
        return await InsertAttemptAsync(connection, transaction: null, attempt);
    }

    /// <inheritdoc />
    public async Task<AttemptAnswer> CreateAnswerAsync(AttemptAnswer answer)
    {
        await using var connection = await _db.OpenConnectionAsync();
        return await InsertAnswerAsync(connection, transaction: null, answer);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AttemptAnswer>> CreateAnswersAsync(IEnumerable<AttemptAnswer> answers)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
            return answerList;

        await using var connection = await _db.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var answer in answerList)
            {
                await InsertAnswerAsync(connection, (MySqlTransaction)transaction, answer);
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
    public async Task<QuizAttempt> CreateAttemptWithAnswersAndAwardXpAsync(
        QuizAttempt attempt,
        IEnumerable<AttemptAnswer> answers,
        int xpEarned)
    {
        var answerList = answers.ToList();

        await using var connection = await _db.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var createdAttempt = await InsertAttemptAsync(connection, (MySqlTransaction)transaction, attempt);

            foreach (var answer in answerList)
            {
                answer.AttemptId = createdAttempt.AttemptId;
                await InsertAnswerAsync(connection, (MySqlTransaction)transaction, answer);
            }

            if (xpEarned > 0)
            {
                await using var xpCmd = new MySqlCommand(AddUserXpSql, connection, (MySqlTransaction)transaction);
                xpCmd.Parameters.AddWithValue("@XpEarned", xpEarned);
                xpCmd.Parameters.AddWithValue("@UserId", createdAttempt.UserId);

                var rowsAffected = await xpCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    throw new KeyNotFoundException($"User with ID {createdAttempt.UserId} was not found.");
            }

            await transaction.CommitAsync();
            return createdAttempt;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<QuizAttempt> InsertAttemptAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        QuizAttempt attempt)
    {
        await using var cmd = new MySqlCommand(InsertAttemptSql, connection, transaction);

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

    private static async Task<AttemptAnswer> InsertAnswerAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        AttemptAnswer answer)
    {
        await using var cmd = new MySqlCommand(InsertAnswerSql, connection, transaction);

        cmd.Parameters.AddWithValue("@AttemptId",      answer.AttemptId);
        cmd.Parameters.AddWithValue("@QuestionId",     answer.QuestionId);
        cmd.Parameters.AddWithValue("@SelectedOption", answer.SelectedOption);
        cmd.Parameters.AddWithValue("@IsCorrect",      answer.IsCorrect);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        answer.AnswerId = insertedId;
        return answer;
    }
}
