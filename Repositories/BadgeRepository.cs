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
            SELECT badge_id, badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id, created_at
            FROM badges
            ORDER BY xp_threshold ASC, badge_id ASC;";

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
            SELECT badge_id, badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id, created_at
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
            SELECT b.badge_id, b.badge_name, b.badge_description, b.xp_threshold, b.icon_type, b.icon_color, b.unlock_hint, b.algorithm_id, b.created_at
            FROM badges b
            INNER JOIN user_badges ub ON b.badge_id = ub.badge_id
            WHERE ub.user_id = @UserId
            ORDER BY b.xp_threshold ASC, b.badge_id ASC;";

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
            SELECT b.badge_id, b.badge_name, b.badge_description, b.xp_threshold, b.icon_type, b.icon_color, b.unlock_hint, b.algorithm_id, b.created_at
            FROM badges b
            WHERE b.algorithm_id IS NULL
            AND b.xp_threshold <= (SELECT XpTotal FROM Users WHERE Id = @UserId)
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
            INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint)
            VALUES (@BadgeName, @BadgeDescription, @XpThreshold, @IconType, @IconColor, @UnlockHint);
            SELECT LAST_INSERT_ID();";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@BadgeName", badge.BadgeName);
        cmd.Parameters.AddWithValue("@BadgeDescription", badge.BadgeDescription);
        cmd.Parameters.AddWithValue("@XpThreshold", badge.XpThreshold);
        cmd.Parameters.AddWithValue("@IconType", badge.IconType);
        cmd.Parameters.AddWithValue("@IconColor", badge.IconColor);
        cmd.Parameters.AddWithValue("@UnlockHint", badge.UnlockHint);

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
                xp_threshold = @XpThreshold,
                icon_type = @IconType,
                icon_color = @IconColor,
                unlock_hint = @UnlockHint
            WHERE badge_id = @BadgeId;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("@BadgeId", badgeId);
        cmd.Parameters.AddWithValue("@BadgeName", badge.BadgeName);
        cmd.Parameters.AddWithValue("@BadgeDescription", badge.BadgeDescription);
        cmd.Parameters.AddWithValue("@XpThreshold", badge.XpThreshold);
        cmd.Parameters.AddWithValue("@IconType", badge.IconType);
        cmd.Parameters.AddWithValue("@IconColor", badge.IconColor);
        cmd.Parameters.AddWithValue("@UnlockHint", badge.UnlockHint);

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

    /// <inheritdoc />
    public async Task<IEnumerable<Badge>> GetUnlockedAlgorithmBadgesByUserIdAsync(int userId)
    {
        const string sql = @"
            SELECT b.badge_id, b.badge_name, b.badge_description, b.xp_threshold, b.icon_type, b.icon_color, b.unlock_hint, b.algorithm_id, b.created_at
            FROM badges b
            WHERE b.algorithm_id IS NOT NULL
            AND b.badge_id NOT IN (
                SELECT badge_id FROM user_badges WHERE user_id = @UserId
            )
            AND b.algorithm_id IN (
                SELECT q.algorithm_id
                FROM quiz_attempts qa
                INNER JOIN quizzes q ON q.quiz_id = qa.quiz_id
                WHERE qa.user_id = @UserId AND qa.passed = 1
            )
            ORDER BY b.badge_id ASC;";

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
            IconType = (string)(reader["icon_type"] ?? "star"),
            IconColor = (string)(reader["icon_color"] ?? "#8f8f3e"),
            UnlockHint = (string)(reader["unlock_hint"] ?? "Locked"),
            AlgorithmId = reader["algorithm_id"] == DBNull.Value ? null : (int?)Convert.ToInt32(reader["algorithm_id"]),
            CreatedAt = (DateTime)reader["created_at"]
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(Badge Badge, DateTime AwardedAt)>> GetEarnedBadgesWithAwardDateAsync(int userId)
    {
        const string sql = @"
            SELECT b.badge_id, b.badge_name, b.badge_description, b.xp_threshold, b.icon_type, b.icon_color, b.unlock_hint, b.algorithm_id, b.created_at,
                   ub.awarded_at
            FROM badges b
            INNER JOIN user_badges ub ON b.badge_id = ub.badge_id
            WHERE ub.user_id = @UserId
            ORDER BY b.xp_threshold ASC, b.badge_id ASC;";

        await using var connection = await _db.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var results = new List<(Badge Badge, DateTime AwardedAt)>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var badge = MapBadge(reader);
            var awardedAt = (DateTime)reader["awarded_at"];
            results.Add((badge, awardedAt));
        }

        return results;
    }
}
