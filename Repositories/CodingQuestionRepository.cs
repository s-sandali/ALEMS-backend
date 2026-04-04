using backend.Data;
using backend.Models;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation of <see cref="ICodingQuestionRepository"/>.
/// Uses parameterized queries to prevent SQL injection.
/// </summary>
public class CodingQuestionRepository : ICodingQuestionRepository
{
    private readonly DatabaseHelper _db;

    public CodingQuestionRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CodingQuestion>> GetAllAsync()
    {
        const string sql = @"
            SELECT id, title, description, input_example, expected_output, difficulty
            FROM coding_questions
            ORDER BY id ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd        = new MySqlCommand(sql, connection);
        await using var reader     = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        var results = new List<CodingQuestion>();
        while (await reader.ReadAsync())
            results.Add(MapCodingQuestion(reader));

        return results;
    }

    /// <inheritdoc />
    public async Task<CodingQuestion?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT id, title, description, input_example, expected_output, difficulty
            FROM coding_questions
            WHERE id = @Id
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd        = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapCodingQuestion(reader);
    }

    /// <inheritdoc />
    public async Task<CodingQuestion> CreateAsync(CodingQuestion question)
    {
        const string sql = @"
            INSERT INTO coding_questions (title, description, input_example, expected_output, difficulty)
            VALUES (@Title, @Description, @InputExample, @ExpectedOutput, @Difficulty);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd        = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@Title",          question.Title);
        cmd.Parameters.AddWithValue("@Description",    question.Description);
        cmd.Parameters.AddWithValue("@InputExample",   (object?)question.InputExample   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ExpectedOutput", (object?)question.ExpectedOutput ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Difficulty",     question.Difficulty);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        var created = await GetByIdAsync(insertedId);
        return created ?? question;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(CodingQuestion question)
    {
        const string sql = @"
            UPDATE coding_questions
            SET title           = @Title,
                description     = @Description,
                input_example   = @InputExample,
                expected_output = @ExpectedOutput,
                difficulty      = @Difficulty
            WHERE id = @Id;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd        = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@Title",          question.Title);
        cmd.Parameters.AddWithValue("@Description",    question.Description);
        cmd.Parameters.AddWithValue("@InputExample",   (object?)question.InputExample   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ExpectedOutput", (object?)question.ExpectedOutput ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Difficulty",     question.Difficulty);
        cmd.Parameters.AddWithValue("@Id",             question.Id);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = "DELETE FROM coding_questions WHERE id = @Id;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd        = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <summary>
    /// Maps the current row of a <see cref="MySqlDataReader"/> to a <see cref="CodingQuestion"/> object.
    /// </summary>
    private static CodingQuestion MapCodingQuestion(MySqlDataReader reader)
    {
        var inputOrdinal    = reader.GetOrdinal("input_example");
        var expectedOrdinal = reader.GetOrdinal("expected_output");

        return new CodingQuestion
        {
            Id             = reader.GetInt32("id"),
            Title          = reader.GetString("title"),
            Description    = reader.GetString("description"),
            InputExample   = reader.IsDBNull(inputOrdinal)    ? null : reader.GetString(inputOrdinal),
            ExpectedOutput = reader.IsDBNull(expectedOrdinal) ? null : reader.GetString(expectedOrdinal),
            Difficulty     = reader.GetString("difficulty")
        };
    }
}
