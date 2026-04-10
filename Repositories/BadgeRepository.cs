using backend.Data;
using backend.Models;
using MySql.Data.MySqlClient;

namespace backend.Repositories;

/// <summary>
/// ADO.NET implementation of <see cref="IBadgeRepository"/>.
/// Uses parameterized queries to prevent SQL injection.
/// </summary>
public class BadgeRepository : IBadgeRepository
{
    private readonly DatabaseHelper _db;

    public BadgeRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Badge>> GetAllAsync()
    {
        const string sql = @"
            SELECT badge_id, badge_name, badge_description, xp_threshold, created_at
            FROM badges
            ORDER BY xp_threshold ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        var badges = new List<Badge>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            badges.Add(MapBadge(reader));
        }

        return badges;
    }

    /// <inheritdoc />
    public async Task<Badge?> GetByIdAsync(int badgeId)
    {
        const string sql = @"
            SELECT badge_id, badge_name, badge_description, xp_threshold, created_at
            FROM badges
            WHERE badge_id = @BadgeId
            LIMIT 1;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@BadgeId", badgeId);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapBadge(reader);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Badge>> GetEarnedBadgesByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT b.badge_id, b.badge_name, b.badge_description, b.xp_threshold, b.created_at
            FROM badges b
            INNER JOIN user_badges ub ON b.badge_id = ub.badge_id
            WHERE ub.user_id = @UserId
            ORDER BY b.xp_threshold ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var badges = new List<Badge>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            badges.Add(MapBadge(reader));
        }

        return badges;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Badge>> GetUnlockedBadgesByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT b.badge_id, b.badge_name, b.badge_description, b.xp_threshold, b.created_at
            FROM badges b
            WHERE b.xp_threshold <= (SELECT xp_total FROM users WHERE id = @UserId)
            AND b.badge_id NOT IN (
                SELECT badge_id FROM user_badges WHERE user_id = @UserId
            )
            ORDER BY b.xp_threshold ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var badges = new List<Badge>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            badges.Add(MapBadge(reader));
        }

        return badges;
    }

    /// <inheritdoc />
    public async Task<Badge> CreateAsync(Badge badge)
    {
        const string sql = @"
            INSERT INTO badges (badge_name, badge_description, xp_threshold)
            VALUES (@BadgeName, @BadgeDescription, @XpThreshold);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@BadgeName", badge.BadgeName);
        cmd.Parameters.AddWithValue("@BadgeDescription", badge.BadgeDescription);
        cmd.Parameters.AddWithValue("@XpThreshold", badge.XpThreshold);

        var id = (long?)await cmd.ExecuteScalarAsync() ?? 0;
        badge.BadgeId = (int)id;

        return badge;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(int badgeId, Badge badge)
    {
        const string sql = @"
            UPDATE badges
            SET badge_name = @BadgeName,
                badge_description = @BadgeDescription,
                xp_threshold = @XpThreshold
            WHERE badge_id = @BadgeId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@BadgeId", badgeId);
        cmd.Parameters.AddWithValue("@BadgeName", badge.BadgeName);
        cmd.Parameters.AddWithValue("@BadgeDescription", badge.BadgeDescription);
        cmd.Parameters.AddWithValue("@XpThreshold", badge.XpThreshold);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int badgeId)
    {
        const string sql = "DELETE FROM badges WHERE badge_id = @BadgeId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@BadgeId", badgeId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> AwardBadgeToUserAsync(int userId, int badgeId)
    {
        const string sql = @"
            INSERT INTO user_badges (user_id, badge_id)
            VALUES (@UserId, @BadgeId);";

        try
        {
            await using var connection = await _db.OpenConnectionAsync();
            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@BadgeId", badgeId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (MySqlException ex) when (ex.Number == 1062) // Duplicate key error
        {
            // Badge already awarded to this user
            return false;
        }
        catch (MySqlException ex) when (ex.Number == 1452) // Foreign key constraint fails
        {
            // Invalid user_id or badge_id
            return false;
        }
    }

    /// <summary>
    /// Maps a MySqlDataReader row to a Badge object.
    /// </summary>
    private static Badge MapBadge(MySqlDataReader reader)
    {
        return new Badge
        {
            BadgeId = (int)reader["badge_id"],
            BadgeName = (string)reader["badge_name"],
            BadgeDescription = (string)reader["badge_description"],
            XpThreshold = (int)(uint)reader["xp_threshold"],  // xp_threshold is UNSIGNED INT
            CreatedAt = (DateTime)reader["created_at"]
        };
    }
}
