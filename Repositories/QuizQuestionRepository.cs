using backend.Data;
using backend.Models;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation of <see cref="IQuizQuestionRepository"/>.
/// Uses parameterized queries to prevent SQL injection.
/// </summary>
public class QuizQuestionRepository : IQuizQuestionRepository
{
    private readonly DatabaseHelper _db;

    public QuizQuestionRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuizQuestion>> GetByQuizIdAsync(int quizId)
    {
        const string sql = @"
            SELECT question_id, quiz_id, question_type, question_text,
                   option_a, option_b, option_c, option_d,
                   correct_option, difficulty, explanation,
                   order_index, is_active, created_at
            FROM quiz_questions
            WHERE quiz_id = @QuizId AND is_active = TRUE
            ORDER BY order_index ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@QuizId", quizId);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        var questions = new List<QuizQuestion>();
        while (await reader.ReadAsync())
            questions.Add(MapQuestion(reader));

        return questions;
    }

    /// <inheritdoc />
    public async Task<QuizQuestion?> GetByIdAsync(int questionId)
    {
        const string sql = @"
            SELECT question_id, quiz_id, question_type, question_text,
                   option_a, option_b, option_c, option_d,
                   correct_option, difficulty, explanation,
                   order_index, is_active, created_at
            FROM quiz_questions
            WHERE question_id = @QuestionId
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@QuestionId", questionId);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapQuestion(reader);
    }

    /// <inheritdoc />
    public async Task<QuizQuestion> CreateAsync(QuizQuestion question)
    {
        const string sql = @"
            INSERT INTO quiz_questions
                (quiz_id, question_type, question_text,
                 option_a, option_b, option_c, option_d,
                 correct_option, difficulty, explanation,
                 order_index, is_active, created_at)
            VALUES
                (@QuizId, @QuestionType, @QuestionText,
                 @OptionA, @OptionB, @OptionC, @OptionD,
                 @CorrectOption, @Difficulty, @Explanation,
                 @OrderIndex, @IsActive, @CreatedAt);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@QuizId",        question.QuizId);
        cmd.Parameters.AddWithValue("@QuestionType",  question.QuestionType);
        cmd.Parameters.AddWithValue("@QuestionText",  question.QuestionText);
        cmd.Parameters.AddWithValue("@OptionA",       question.OptionA);
        cmd.Parameters.AddWithValue("@OptionB",       question.OptionB);
        cmd.Parameters.AddWithValue("@OptionC",       question.OptionC);
        cmd.Parameters.AddWithValue("@OptionD",       question.OptionD);
        cmd.Parameters.AddWithValue("@CorrectOption", question.CorrectOption);
        cmd.Parameters.AddWithValue("@Difficulty",    question.Difficulty);
        cmd.Parameters.AddWithValue("@Explanation",   (object?)question.Explanation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OrderIndex",    question.OrderIndex);
        cmd.Parameters.AddWithValue("@IsActive",      question.IsActive);
        cmd.Parameters.AddWithValue("@CreatedAt",     DateTime.UtcNow);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        var created = await GetByIdAsync(insertedId);
        return created ?? question;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(QuizQuestion question)
    {
        const string sql = @"
            UPDATE quiz_questions
            SET question_type  = @QuestionType,
                question_text  = @QuestionText,
                option_a       = @OptionA,
                option_b       = @OptionB,
                option_c       = @OptionC,
                option_d       = @OptionD,
                correct_option = @CorrectOption,
                difficulty     = @Difficulty,
                explanation    = @Explanation,
                order_index    = @OrderIndex,
                is_active      = @IsActive
            WHERE question_id = @QuestionId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@QuestionType",  question.QuestionType);
        cmd.Parameters.AddWithValue("@QuestionText",  question.QuestionText);
        cmd.Parameters.AddWithValue("@OptionA",       question.OptionA);
        cmd.Parameters.AddWithValue("@OptionB",       question.OptionB);
        cmd.Parameters.AddWithValue("@OptionC",       question.OptionC);
        cmd.Parameters.AddWithValue("@OptionD",       question.OptionD);
        cmd.Parameters.AddWithValue("@CorrectOption", question.CorrectOption);
        cmd.Parameters.AddWithValue("@Difficulty",    question.Difficulty);
        cmd.Parameters.AddWithValue("@Explanation",   (object?)question.Explanation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OrderIndex",    question.OrderIndex);
        cmd.Parameters.AddWithValue("@IsActive",      question.IsActive);
        cmd.Parameters.AddWithValue("@QuestionId",    question.QuestionId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int questionId)
    {
        const string sql = @"
            UPDATE quiz_questions
            SET is_active = FALSE
            WHERE question_id = @QuestionId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@QuestionId", questionId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <summary>
    /// Maps the current row of a <see cref="MySqlDataReader"/> to a <see cref="QuizQuestion"/>.
    /// </summary>
    private static QuizQuestion MapQuestion(MySqlDataReader reader)
    {
        var explanationOrdinal = reader.GetOrdinal("explanation");

        return new QuizQuestion
        {
            QuestionId   = reader.GetInt32("question_id"),
            QuizId       = reader.GetInt32("quiz_id"),
            QuestionType = reader.GetString("question_type"),
            QuestionText = reader.GetString("question_text"),
            OptionA      = reader.GetString("option_a"),
            OptionB      = reader.GetString("option_b"),
            OptionC      = reader.GetString("option_c"),
            OptionD      = reader.GetString("option_d"),
            CorrectOption = reader.GetString("correct_option"),
            Difficulty   = reader.GetString("difficulty"),
            Explanation  = reader.IsDBNull(explanationOrdinal) ? null : reader.GetString(explanationOrdinal),
            OrderIndex   = reader.GetInt32("order_index"),
            IsActive     = reader.GetBoolean("is_active"),
            CreatedAt    = reader.GetDateTime("created_at")
        };
    }
}
