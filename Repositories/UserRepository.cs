using backend.Data;
using backend.DTOs;
using backend.Models;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation of <see cref="IUserRepository"/>.
/// Uses parameterized queries to prevent SQL injection.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly DatabaseHelper _db;

    public UserRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<User?> GetByClerkUserIdAsync(string clerkUserId)
    {
        const string sql = @"
            SELECT Id, ClerkUserId, Email, Role,
                   XpTotal, IsActive, CreatedAt
            FROM Users
            WHERE ClerkUserId = @ClerkUserId
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ClerkUserId", clerkUserId);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapUser(reader);
    }

    /// <inheritdoc />
    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = @"
            SELECT Id, ClerkUserId, Email, Role,
                   XpTotal, IsActive, CreatedAt
            FROM Users
            WHERE Email = @Email
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Email", email);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapUser(reader);
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(User user)
    {
        const string sql = @"
            INSERT INTO Users (ClerkUserId, Email, Role, XpTotal, IsActive, CreatedAt)
            VALUES (@ClerkUserId, @Email, @Role, @XpTotal, @IsActive, @CreatedAt);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@ClerkUserId",
            string.IsNullOrEmpty(user.ClerkUserId) ? DBNull.Value : user.ClerkUserId);
        cmd.Parameters.AddWithValue("@Email", user.Email);
        cmd.Parameters.AddWithValue("@Role", user.Role);
        cmd.Parameters.AddWithValue("@XpTotal", user.XpTotal);
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        user.UserId = insertedId;

        // Re-fetch to get server-generated timestamps
        var created = await GetByIdAsync(insertedId);
        return created ?? user;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, ClerkUserId, Email, Role,
                   XpTotal, IsActive, CreatedAt
            FROM Users
            ORDER BY CreatedAt DESC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        var users = new List<User>();
        while (await reader.ReadAsync())
        {
            users.Add(MapUser(reader));
        }

        return users;
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, ClerkUserId, Email, Role,
                   XpTotal, IsActive, CreatedAt
            FROM Users
            WHERE Id = @Id
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapUser(reader);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(int id, string role, bool isActive)
    {
        const string sql = @"
            UPDATE Users
            SET Role = @Role,
                IsActive = @IsActive
            WHERE Id = @Id;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        
        cmd.Parameters.AddWithValue("@Role", role);
        cmd.Parameters.AddWithValue("@IsActive", isActive);
        cmd.Parameters.AddWithValue("@Id", id);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> LinkClerkUserIdAsync(int userId, string clerkUserId)
    {
        const string sql = @"
            UPDATE Users
            SET ClerkUserId = @ClerkUserId
            WHERE Id = @Id;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ClerkUserId", clerkUserId);
        cmd.Parameters.AddWithValue("@Id", userId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = @"
            UPDATE Users
            SET IsActive = false
            WHERE Id = @Id;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> AddXpAsync(int userId, int xpEarned)
    {
        const string sql = @"
            UPDATE Users
            SET XpTotal = XpTotal + @XpEarned
            WHERE Id = @Id;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@XpEarned", xpEarned);
        cmd.Parameters.AddWithValue("@Id", userId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LeaderboardEntryDto>> GetTopUsersAsync(int limit)
    {
        const string sql = @"
            SELECT Id, Email, ClerkUserId, XpTotal
            FROM Users
            WHERE IsActive = 1
            ORDER BY XpTotal DESC, Id ASC
            LIMIT @Limit;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Limit", limit);

        var entries = new List<LeaderboardEntryDto>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        int rank = 1;
        while (await reader.ReadAsync())
        {
            var userId = reader.GetInt32("Id");
            var email = reader.IsDBNull(reader.GetOrdinal("Email"))
                ? null
                : reader.GetString("Email");
            var clerkUserId = reader.IsDBNull(reader.GetOrdinal("ClerkUserId"))
                ? null
                : reader.GetString("ClerkUserId");

            entries.Add(new LeaderboardEntryDto
            {
                UserId   = userId,
                Username = BuildDisplayName(email, userId, clerkUserId),
                XpTotal  = reader.GetInt32("XpTotal"),
                Rank     = rank++
            });
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<int> GetUserRankAsync(int userId)
    {
        const string sql = @"
            SELECT COUNT(*) + 1 AS user_rank
            FROM Users
            WHERE IsActive = 1
              AND XpTotal > (
                  SELECT XpTotal FROM Users WHERE Id = @UserId AND IsActive = 1
              );";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        // ExecuteScalar returns 1 when the user is not found (sub-query returns NULL → no rows match)
        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value ? 1 : Convert.ToInt32(result);
    }

    /// <summary>
    /// Maps the current row of a <see cref="MySqlDataReader"/> to a <see cref="User"/> object.
    /// Handles nullable clerk_user_id for admin-created users.
    /// </summary>
    private static User MapUser(MySqlDataReader reader)
    {
        var clerkOrdinal = reader.GetOrdinal("ClerkUserId");
        var userId = reader.GetInt32("Id");
        var clerkUserId = reader.IsDBNull(clerkOrdinal) ? string.Empty : reader.GetString(clerkOrdinal);
        var email = reader.GetString("Email");

        return new User
        {
            UserId = userId,
            ClerkUserId = clerkUserId,
            Email = email,
            Username = BuildDisplayName(email, userId, clerkUserId),
            Role = reader.GetString("Role"),
            XpTotal = reader.GetInt32("XpTotal"),
            IsActive = reader.GetBoolean("IsActive"),
            CreatedAt = reader.GetDateTime("CreatedAt"),
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string BuildDisplayName(string? email, int userId, string? clerkUserId)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var atIndex = email.IndexOf('@');
            var prefix = atIndex > 0 ? email[..atIndex] : email;
            if (!string.IsNullOrWhiteSpace(prefix))
                return prefix;
        }

        if (!string.IsNullOrWhiteSpace(clerkUserId))
            return clerkUserId;

        return $"user{userId}";
    }
}
