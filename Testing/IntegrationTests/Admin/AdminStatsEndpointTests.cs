using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using backend.Data;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using Xunit;

namespace IntegrationTests.Admin;

public class AdminStatsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminStatsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "BE-IT-ADM-01 — GET /api/admin/stats returns correct aggregation")]
    public async Task GetAdminStats_WithAdminToken_ReturnsAccurateAggregation()
    {
        var tag = BuildTag("admstats");
        await using var db = await OpenConnectionAsync();

        try
        {
            // Capture baseline BEFORE seeding so parallel-test activity doesn't skew expectations.
            // We know exactly what SeedStatsDataAsync inserts: 2 users, 1 quiz, 3 attempts (2 passed).
            var baseline = await ReadExpectedStatsFromDatabaseAsync(db);

            await SeedStatsDataAsync(db, tag);

            var expectedUsers    = baseline.TotalUsers    + 2;
            var expectedQuizzes  = baseline.TotalQuizzes  + 1;
            var expectedAttempts = baseline.TotalAttempts + 3;
            var expectedPassed   = (int)Math.Round(baseline.AveragePassRate / 100.0 * baseline.TotalAttempts) + 2;
            var expectedPassRate = expectedAttempts > 0
                ? (expectedPassed * 100.0) / expectedAttempts
                : 0.0;

            var client = BuildAuthorizedClient(TestAuthHandler.AdminToken);
            var response = await client.GetAsync("/api/admin/stats");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;

            root.GetProperty("totalUsers").GetInt32().Should().Be(expectedUsers);
            root.GetProperty("totalQuizzes").GetInt32().Should().Be(expectedQuizzes);
            root.GetProperty("totalAttempts").GetInt32().Should().Be(expectedAttempts);
            root.GetProperty("averagePassRate").GetDouble().Should().BeApproximately(expectedPassRate, 0.01);
        }
        finally
        {
            await CleanupAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-ADM-02 — GET /api/admin/stats returns 403 for student token")]
    public async Task GetAdminStats_WithStudentToken_ReturnsForbidden()
    {
        var client = BuildAuthorizedClient(TestAuthHandler.UserToken);

        var response = await client.GetAsync("/api/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-ADM-03 — GET /api/admin/stats returns 401 when unauthenticated")]
    public async Task GetAdminStats_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient BuildAuthorizedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<MySqlConnection> OpenConnectionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseHelper>();
        return await db.OpenConnectionAsync();
    }

    private static async Task SeedStatsDataAsync(MySqlConnection db, string tag)
    {
        var adminUserId = await InsertUserAsync(db, $"{tag}_admin_sub", $"{tag}.admin@example.com", role: "Admin");
        var studentUserId = await InsertUserAsync(db, $"{tag}_student_sub", $"{tag}.student@example.com", role: "User");

        var algorithmId = await InsertAlgorithmAsync(db, tag);
        var quizId = await InsertQuizAsync(db, tag, algorithmId, adminUserId);

        await InsertQuizAttemptAsync(db, studentUserId, quizId, score: 8, totalQuestions: 10, xpEarned: 20, passed: true);
        await InsertQuizAttemptAsync(db, studentUserId, quizId, score: 6, totalQuestions: 10, xpEarned: 10, passed: false);
        await InsertQuizAttemptAsync(db, adminUserId, quizId, score: 9, totalQuestions: 10, xpEarned: 30, passed: true);
    }

    private static async Task<(int TotalUsers, int TotalQuizzes, int TotalAttempts, double AveragePassRate)> ReadExpectedStatsFromDatabaseAsync(MySqlConnection db)
    {
        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM Users) AS total_users,
                (SELECT COUNT(*) FROM quizzes) AS total_quizzes,
                (SELECT COUNT(*) FROM quiz_attempts) AS total_attempts,
                (SELECT COALESCE(SUM(CASE WHEN passed = 1 THEN 1 ELSE 0 END), 0) FROM quiz_attempts) AS passed_attempts;";

        await using var cmd = new MySqlCommand(sql, db);
        await using var reader = await cmd.ExecuteReaderAsync();

        await reader.ReadAsync();

        var totalUsers = reader.GetInt32(reader.GetOrdinal("total_users"));
        var totalQuizzes = reader.GetInt32(reader.GetOrdinal("total_quizzes"));
        var totalAttempts = reader.GetInt32(reader.GetOrdinal("total_attempts"));
        var passedAttempts = reader.GetInt32(reader.GetOrdinal("passed_attempts"));

        var averagePassRate = totalAttempts > 0
            ? (passedAttempts * 100.0) / totalAttempts
            : 0.0;

        return (totalUsers, totalQuizzes, totalAttempts, averagePassRate);
    }

    private static async Task<int> InsertUserAsync(MySqlConnection db, string clerkSub, string email, string role)
    {
        const string sql = @"
            INSERT INTO Users (ClerkUserId, Email, Role, XpTotal, IsActive, CreatedAt)
            VALUES (@ClerkSub, @Email, @Role, 0, 1, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@ClerkSub", clerkSub);
        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@Role", role);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<int> InsertAlgorithmAsync(MySqlConnection db, string tag)
    {
        const string sql = @"
            INSERT INTO algorithms
                (name, category, description, time_complexity_best, time_complexity_average, time_complexity_worst, created_at)
            VALUES
                (@Name, 'Sorting', 'Integration-test algorithm', 'O(n)', 'O(n log n)', 'O(n^2)', UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@Name", $"{tag}-algorithm");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<int> InsertQuizAsync(MySqlConnection db, string tag, int algorithmId, int createdBy)
    {
        const string sql = @"
            INSERT INTO quizzes
                (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active, created_at, updated_at)
            VALUES
                (@AlgorithmId, @CreatedBy, @Title, 'Integration test quiz', NULL, 70, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@AlgorithmId", algorithmId);
        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
        cmd.Parameters.AddWithValue("@Title", $"{tag}-quiz");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task InsertQuizAttemptAsync(
        MySqlConnection db,
        int userId,
        int quizId,
        int score,
        int totalQuestions,
        int xpEarned,
        bool passed)
    {
        const string sql = @"
            INSERT INTO quiz_attempts
                (user_id, quiz_id, score, total_questions, xp_earned, passed, started_at, completed_at)
            VALUES
                (@UserId, @QuizId, @Score, @TotalQuestions, @XpEarned, @Passed, UTC_TIMESTAMP(), UTC_TIMESTAMP());";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@QuizId", quizId);
        cmd.Parameters.AddWithValue("@Score", score);
        cmd.Parameters.AddWithValue("@TotalQuestions", totalQuestions);
        cmd.Parameters.AddWithValue("@XpEarned", xpEarned);
        cmd.Parameters.AddWithValue("@Passed", passed);

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CleanupAsync(MySqlConnection db, string tag)
    {
        var prefix = $"{tag}%";

        const string deleteAttemptAnswersSql = @"
            DELETE aa
            FROM attempt_answers aa
            INNER JOIN quiz_attempts qa ON qa.attempt_id = aa.attempt_id
            INNER JOIN quizzes q ON q.quiz_id = qa.quiz_id
            WHERE q.title LIKE @Prefix;";

        const string deleteAttemptsSql = @"
            DELETE qa
            FROM quiz_attempts qa
            INNER JOIN quizzes q ON q.quiz_id = qa.quiz_id
            WHERE q.title LIKE @Prefix;";

        const string deleteQuizzesSql = "DELETE FROM quizzes WHERE title LIKE @Prefix;";
        const string deleteAlgorithmsSql = "DELETE FROM algorithms WHERE name LIKE @Prefix;";

        const string deleteUserBadgesSql = @"
            DELETE ub
            FROM user_badges ub
            INNER JOIN Users u ON u.Id = ub.user_id
            WHERE u.email LIKE @Prefix OR u.clerkUserId LIKE @Prefix;";

        const string deleteUsersSql = @"
            DELETE FROM Users
            WHERE email LIKE @Prefix OR clerkUserId LIKE @Prefix;";

        foreach (var sql in new[]
                 {
                     deleteAttemptAnswersSql,
                     deleteAttemptsSql,
                     deleteQuizzesSql,
                     deleteAlgorithmsSql,
                     deleteUserBadgesSql,
                     deleteUsersSql
                 })
        {
            await using var cmd = new MySqlCommand(sql, db);
            cmd.Parameters.AddWithValue("@Prefix", prefix);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string BuildTag(string id)
        => $"it_{id}_{Guid.NewGuid():N}";
}
