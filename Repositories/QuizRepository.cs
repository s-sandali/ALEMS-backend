using backend.Data;
using backend.Models;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation of <see cref="IQuizRepository"/>.
/// Uses parameterized queries to prevent SQL injection.
/// </summary>
public class QuizRepository : IQuizRepository
{
    private readonly DatabaseHelper _db;

    public QuizRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Quiz>> GetAllAsync()
    {
        const string sql = @"
            SELECT quiz_id, algorithm_id, created_by, title, description,
                   time_limit_mins, pass_score, is_active, created_at, updated_at
            FROM quizzes
            ORDER BY created_at DESC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        var quizzes = new List<Quiz>();
        while (await reader.ReadAsync())
            quizzes.Add(MapQuiz(reader));

        return quizzes;
    }

    /// <inheritdoc />
    public async Task<Quiz?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT quiz_id, algorithm_id, created_by, title, description,
                   time_limit_mins, pass_score, is_active, created_at, updated_at
            FROM quizzes
            WHERE quiz_id = @QuizId
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@QuizId", id);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapQuiz(reader);
    }

    /// <inheritdoc />
    public async Task<Quiz> CreateAsync(Quiz quiz)
    {
        const string sql = @"
            INSERT INTO quizzes
                (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active, created_at, updated_at)
            VALUES
                (@AlgorithmId, @CreatedBy, @Title, @Description, @TimeLimitMins, @PassScore, @IsActive, @CreatedAt, @UpdatedAt);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        var now = DateTime.UtcNow;
        cmd.Parameters.AddWithValue("@AlgorithmId",   quiz.AlgorithmId);
        cmd.Parameters.AddWithValue("@CreatedBy",     quiz.CreatedBy);
        cmd.Parameters.AddWithValue("@Title",         quiz.Title);
        cmd.Parameters.AddWithValue("@Description",   (object?)quiz.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TimeLimitMins", (object?)quiz.TimeLimitMins ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PassScore",     quiz.PassScore);
        cmd.Parameters.AddWithValue("@IsActive",      quiz.IsActive);
        cmd.Parameters.AddWithValue("@CreatedAt",     now);
        cmd.Parameters.AddWithValue("@UpdatedAt",     now);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        var created = await GetByIdAsync(insertedId);
        return created ?? quiz;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Quiz quiz)
    {
        const string sql = @"
            UPDATE quizzes
            SET title           = @Title,
                description     = @Description,
                time_limit_mins = @TimeLimitMins,
                pass_score      = @PassScore,
                is_active       = @IsActive,
                updated_at      = @UpdatedAt
            WHERE quiz_id = @QuizId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@Title",         quiz.Title);
        cmd.Parameters.AddWithValue("@Description",   (object?)quiz.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TimeLimitMins", (object?)quiz.TimeLimitMins ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PassScore",     quiz.PassScore);
        cmd.Parameters.AddWithValue("@IsActive",      quiz.IsActive);
        cmd.Parameters.AddWithValue("@UpdatedAt",     DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@QuizId",        quiz.QuizId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = @"
            UPDATE quizzes
            SET is_active  = false,
                updated_at = @UpdatedAt
            WHERE quiz_id = @QuizId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@QuizId",    id);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <summary>
    /// Maps the current row of a <see cref="MySqlDataReader"/> to a <see cref="Quiz"/> object.
    /// </summary>
    private static Quiz MapQuiz(MySqlDataReader reader)
    {
        var timeLimitOrdinal  = reader.GetOrdinal("time_limit_mins");
        var descriptionOrdinal = reader.GetOrdinal("description");

        return new Quiz
        {
            QuizId        = reader.GetInt32("quiz_id"),
            AlgorithmId   = reader.GetInt32("algorithm_id"),
            CreatedBy     = reader.GetInt32("created_by"),
            Title         = reader.GetString("title"),
            Description   = reader.IsDBNull(descriptionOrdinal) ? null : reader.GetString(descriptionOrdinal),
            TimeLimitMins = reader.IsDBNull(timeLimitOrdinal)  ? null : reader.GetInt32(timeLimitOrdinal),
            PassScore     = reader.GetInt32("pass_score"),
            IsActive      = reader.GetBoolean("is_active"),
            CreatedAt     = reader.GetDateTime("created_at"),
            UpdatedAt     = reader.GetDateTime("updated_at")
        };
    }
}
