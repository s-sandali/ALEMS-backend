using backend.Data;
using backend.Models;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation of <see cref="IAlgorithmRepository"/>.
/// Uses parameterized queries to prevent SQL injection.
/// </summary>
public class AlgorithmRepository : IAlgorithmRepository
{
    private readonly DatabaseHelper _db;

    public AlgorithmRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Algorithm>> GetAllAsync()
    {
        const string sql = @"
            SELECT algorithm_id, name, category, description,
                   time_complexity_best, time_complexity_average, time_complexity_worst,
                   created_at
            FROM algorithms
            ORDER BY name ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        var algorithms = new List<Algorithm>();
        while (await reader.ReadAsync())
        {
            algorithms.Add(MapAlgorithm(reader));
        }

        return algorithms;
    }

    /// <inheritdoc />
    public async Task<Algorithm?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT algorithm_id, name, category, description,
                   time_complexity_best, time_complexity_average, time_complexity_worst,
                   created_at
            FROM algorithms
            WHERE algorithm_id = @AlgorithmId
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@AlgorithmId", id);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapAlgorithm(reader);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Algorithm>> GetByIdsAsync(IEnumerable<int> algorithmIds)
    {
        var idsList = algorithmIds.Distinct().ToList();
        if (idsList.Count == 0)
            return new List<Algorithm>();

        var placeholders = string.Join(",", idsList.Select((_, i) => $"@Id{i}"));
        var sql = $@"
            SELECT algorithm_id, name, category, description,
                   time_complexity_best, time_complexity_average, time_complexity_worst,
                   created_at
            FROM algorithms
            WHERE algorithm_id IN ({placeholders});";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        for (int i = 0; i < idsList.Count; i++)
        {
            cmd.Parameters.AddWithValue($"@Id{i}", idsList[i]);
        }

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        var algorithms = new List<Algorithm>();
        while (await reader.ReadAsync())
        {
            algorithms.Add(MapAlgorithm(reader));
        }

        return algorithms;
    }

    /// <summary>
    /// Maps the current row of a <see cref="MySqlDataReader"/> to an <see cref="Algorithm"/> object.
    /// </summary>
    private static Algorithm MapAlgorithm(MySqlDataReader reader)
    {
        return new Algorithm
        {
            AlgorithmId          = reader.GetInt32("algorithm_id"),
            Name                 = reader.GetString("name"),
            Category             = reader.GetString("category"),
            Description          = reader.GetString("description"),
            TimeComplexityBest   = reader.GetString("time_complexity_best"),
            TimeComplexityAverage = reader.GetString("time_complexity_average"),
            TimeComplexityWorst  = reader.GetString("time_complexity_worst"),
            CreatedAt            = reader.GetDateTime("created_at")
        };
    }
}
