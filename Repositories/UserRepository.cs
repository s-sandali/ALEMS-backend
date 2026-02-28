using backend.Data;
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
            SELECT user_id, clerk_user_id, email, username, role,
                   xp_total, is_active, created_at, updated_at
            FROM Users
            WHERE clerk_user_id = @ClerkUserId
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
    public async Task<User> CreateAsync(User user)
    {
        const string sql = @"
            INSERT INTO Users (clerk_user_id, email, username, role, xp_total, is_active)
            VALUES (@ClerkUserId, @Email, @Username, @Role, @XpTotal, @IsActive);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@ClerkUserId", user.ClerkUserId);
        cmd.Parameters.AddWithValue("@Email", user.Email);
        cmd.Parameters.AddWithValue("@Username", user.Username);
        cmd.Parameters.AddWithValue("@Role", user.Role);
        cmd.Parameters.AddWithValue("@XpTotal", user.XpTotal);
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive);

        var insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        user.UserId = insertedId;

        // Re-fetch to get server-generated timestamps
        var created = await GetByClerkUserIdAsync(user.ClerkUserId);
        return created ?? user;
    }

    /// <summary>
    /// Maps the current row of a <see cref="MySqlDataReader"/> to a <see cref="User"/> object.
    /// </summary>
    private static User MapUser(MySqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetInt32("user_id"),
            ClerkUserId = reader.GetString("clerk_user_id"),
            Email = reader.GetString("email"),
            Username = reader.GetString("username"),
            Role = reader.GetString("role"),
            XpTotal = reader.GetInt32("xp_total"),
            IsActive = reader.GetBoolean("is_active"),
            CreatedAt = reader.GetDateTime("created_at"),
            UpdatedAt = reader.GetDateTime("updated_at")
        };
    }
}
