using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using backend.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Xunit;

namespace IntegrationTests.Students;

public class StudentDashboardEndpointTests : IClassFixture<StudentDashboardWebApplicationFactory>
{
    private readonly StudentDashboardWebApplicationFactory _factory;

    public StudentDashboardEndpointTests(StudentDashboardWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "BE-IT-SD-01 — GET /api/students/{id}/dashboard returns correct XP and badges")]
    public async Task GetDashboard_ReturnsCorrectXpAndBadges()
    {
        var tag = BuildTag("sd01");
        var clerkSub = $"{tag}_clerk_sub";
        const string firstStepsBadge = "First Steps";
        const string quickLearnerBadge = "Quick Learner";
        await using var db = await OpenConnectionAsync();

        try
        {
            var userId = await InsertUserAsync(db, clerkSub, $"{tag}.student@example.com", xpTotal: 130);

            // Insert extra badges to verify dashboard canonicalizes XP badges by threshold.
            await InsertXpBadgeAsync(db, $"{tag}-badge-50", 50);
            await InsertXpBadgeAsync(db, $"{tag}-badge-120", 120);

            var client = BuildAuthorizedClient(clerkSub);
            var response = await client.GetAsync($"/api/students/{userId}/dashboard");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;

            root.GetProperty("status").GetString().Should().Be("success");
            var data = root.GetProperty("data");

            data.GetProperty("studentId").GetInt32().Should().Be(userId);
            data.GetProperty("xpTotal").GetInt32().Should().Be(130);

            var earnedBadges = data.GetProperty("earnedBadges").EnumerateArray().ToList();
            earnedBadges.Any(b => string.Equals(b.GetProperty("name").GetString(), firstStepsBadge, StringComparison.Ordinal)).Should().BeTrue();
            earnedBadges.Any(b => string.Equals(b.GetProperty("name").GetString(), quickLearnerBadge, StringComparison.Ordinal)).Should().BeFalse();

            var allBadges = data.GetProperty("allBadges").EnumerateArray().ToList();
            allBadges.Any(b =>
                string.Equals(b.GetProperty("name").GetString(), firstStepsBadge, StringComparison.Ordinal) &&
                b.GetProperty("earned").GetBoolean()).Should().BeTrue();
            allBadges.Any(b =>
                string.Equals(b.GetProperty("name").GetString(), quickLearnerBadge, StringComparison.Ordinal) &&
                b.GetProperty("earned").GetBoolean()).Should().BeFalse();
        }
        finally
        {
            await CleanupAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SD-02 — GET /api/students/{id}/dashboard returns 403 for mismatched student ID")]
    public async Task GetDashboard_WithMismatchedStudentId_Returns403()
    {
        var tag = BuildTag("sd02");
        var requesterSub = $"{tag}_requester_sub";
        await using var db = await OpenConnectionAsync();

        try
        {
            await InsertUserAsync(db, requesterSub, $"{tag}.requester@example.com", xpTotal: 25);
            var targetUserId = await InsertUserAsync(db, $"{tag}_other_sub", $"{tag}.other@example.com", xpTotal: 80);

            var client = BuildAuthorizedClient(requesterSub);
            var response = await client.GetAsync($"/api/students/{targetUserId}/dashboard");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await CleanupAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SD-03 — GET /api/students/{id}/dashboard returns 401 when unauthenticated")]
    public async Task GetDashboard_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/students/1/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-SD-04 — GET /api/students/{id}/dashboard returns attempt history most-recent-first and page-ready slices")]
    public async Task GetDashboard_AttemptHistory_IsMostRecentFirstAndPageReady()
    {
        var tag = BuildTag("sd04");
        var clerkSub = $"{tag}_student_clerk";
        await using var db = await OpenConnectionAsync();

        try
        {
            var studentId = await InsertUserAsync(db, clerkSub, $"{tag}.student@example.com", xpTotal: 90);
            var adminId = await InsertUserAsync(db, $"{tag}_admin_clerk", $"{tag}.admin@example.com", xpTotal: 0, role: "Admin");

            var algorithmId = await InsertAlgorithmAsync(db, tag);
            var quizId = await InsertQuizAsync(db, tag, algorithmId, adminId);

            var oldestCompletedAt = new DateTime(2026, 04, 10, 8, 0, 0, DateTimeKind.Utc);
            var middleCompletedAt = new DateTime(2026, 04, 11, 8, 0, 0, DateTimeKind.Utc);
            var newestCompletedAt = new DateTime(2026, 04, 12, 8, 0, 0, DateTimeKind.Utc);

            var middleAttemptId = await InsertQuizAttemptAsync(db, studentId, quizId, score: 7, totalQuestions: 10, xpEarned: 30, passed: true, completedAtUtc: middleCompletedAt);
            var oldestAttemptId = await InsertQuizAttemptAsync(db, studentId, quizId, score: 6, totalQuestions: 10, xpEarned: 20, passed: false, completedAtUtc: oldestCompletedAt);
            var newestAttemptId = await InsertQuizAttemptAsync(db, studentId, quizId, score: 9, totalQuestions: 10, xpEarned: 40, passed: true, completedAtUtc: newestCompletedAt);

            var client = BuildAuthorizedClient(clerkSub);
            var response = await client.GetAsync($"/api/students/{studentId}/dashboard");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var history = body.RootElement
                .GetProperty("data")
                .GetProperty("quizAttemptHistory")
                .EnumerateArray()
                .ToList();

            history.Should().HaveCount(3);

            var attemptIds = history
                .Select(item => item.GetProperty("attemptId").GetInt32())
                .ToList();

            attemptIds.Should().Equal(newestAttemptId, middleAttemptId, oldestAttemptId);

            var completedAt = history
                .Select(item => item.GetProperty("completedAt").GetDateTime())
                .ToList();

            completedAt[0].Should().BeOnOrAfter(completedAt[1]);
            completedAt[1].Should().BeOnOrAfter(completedAt[2]);

            const int pageSize = 2;
            var firstPage = history.Take(pageSize).ToList();
            var secondPage = history.Skip(pageSize).Take(pageSize).ToList();

            firstPage.Should().HaveCount(pageSize);
            secondPage.Should().HaveCount(1);

            firstPage.Last().GetProperty("completedAt").GetDateTime()
                .Should().BeOnOrAfter(secondPage.First().GetProperty("completedAt").GetDateTime());
        }
        finally
        {
            await CleanupAsync(db, tag);
        }
    }

    private HttpClient BuildAuthorizedClient(string clerkSub)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", StudentDashboardTestAuthHandler.UserToken);
        client.DefaultRequestHeaders.Remove("X-Test-Sub");
        client.DefaultRequestHeaders.Add("X-Test-Sub", clerkSub);
        return client;
    }

    private async Task<MySqlConnection> OpenConnectionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseHelper>();
        return await db.OpenConnectionAsync();
    }

    private static async Task<int> InsertUserAsync(MySqlConnection db, string clerkSub, string email, int xpTotal, string role = "User")
    {
        const string sql = @"
            INSERT INTO Users (ClerkUserId, Email, Role, XpTotal, IsActive, CreatedAt)
            VALUES (@ClerkSub, @Email, @Role, @XpTotal, 1, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@ClerkSub", clerkSub);
        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@Role", role);
        cmd.Parameters.AddWithValue("@XpTotal", xpTotal);

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

    private static async Task<int> InsertQuizAsync(MySqlConnection db, string tag, int algorithmId, int adminUserId)
    {
        const string sql = @"
            INSERT INTO quizzes
                (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active, created_at, updated_at)
            VALUES
                (@AlgorithmId, @CreatedBy, @Title, 'Integration test quiz', NULL, 70, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@AlgorithmId", algorithmId);
        cmd.Parameters.AddWithValue("@CreatedBy", adminUserId);
        cmd.Parameters.AddWithValue("@Title", $"{tag}-quiz");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<int> InsertQuizAttemptAsync(
        MySqlConnection db,
        int userId,
        int quizId,
        int score,
        int totalQuestions,
        int xpEarned,
        bool passed,
        DateTime completedAtUtc)
    {
        const string sql = @"
            INSERT INTO quiz_attempts
                (user_id, quiz_id, score, total_questions, xp_earned, passed, started_at, completed_at)
            VALUES
                (@UserId, @QuizId, @Score, @TotalQuestions, @XpEarned, @Passed, @StartedAt, @CompletedAt);
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@QuizId", quizId);
        cmd.Parameters.AddWithValue("@Score", score);
        cmd.Parameters.AddWithValue("@TotalQuestions", totalQuestions);
        cmd.Parameters.AddWithValue("@XpEarned", xpEarned);
        cmd.Parameters.AddWithValue("@Passed", passed);
        cmd.Parameters.AddWithValue("@StartedAt", completedAtUtc.AddMinutes(-5));
        cmd.Parameters.AddWithValue("@CompletedAt", completedAtUtc);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task InsertXpBadgeAsync(MySqlConnection db, string badgeName, int threshold)
    {
        const string sql = @"
            INSERT INTO badges
                (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id, created_at)
            VALUES
                (@BadgeName, 'Integration test badge', @Threshold, 'star', '#22AA88', 'Reach threshold', NULL, UTC_TIMESTAMP());";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@BadgeName", badgeName);
        cmd.Parameters.AddWithValue("@Threshold", threshold);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CleanupAsync(MySqlConnection db, string tag)
    {
        var prefix = $"{tag}%";

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
            INNER JOIN users u ON u.id = ub.user_id
            WHERE u.email LIKE @Prefix;";

        const string deleteUsersSql = @"
            DELETE FROM users
            WHERE email LIKE @Prefix
               OR clerkUserId LIKE @Prefix;";

        const string deleteBadgesSql = "DELETE FROM badges WHERE badge_name LIKE @Prefix;";

        foreach (var sql in new[]
                 {
                     deleteAttemptsSql,
                     deleteQuizzesSql,
                     deleteAlgorithmsSql,
                     deleteUserBadgesSql,
                     deleteUsersSql,
                     deleteBadgesSql
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

public sealed class StudentDashboardWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SkipMigrations"] = "true"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = StudentDashboardTestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = StudentDashboardTestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, StudentDashboardTestAuthHandler>(
                StudentDashboardTestAuthHandler.SchemeName, _ => { });
        });
    }
}

public sealed class StudentDashboardTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "StudentDashboardTestAuth";
    public const string UserToken = "student-dashboard-test-user-token";

    public StudentDashboardTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var raw = authHeader.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Missing Bearer prefix."));

        var token = raw["Bearer ".Length..].Trim();
        if (!string.Equals(token, UserToken, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Token is invalid."));

        var sub = Request.Headers.TryGetValue("X-Test-Sub", out var subs)
            ? subs.ToString()
            : "student_dashboard_default_sub";

        if (string.IsNullOrWhiteSpace(sub))
            sub = "student_dashboard_default_sub";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub),
            new("sub", sub),
            new(ClaimTypes.Name, "student-dashboard-test-user"),
            new(ClaimTypes.Email, "student.dashboard.test@example.com"),
            new(ClaimTypes.Role, "User")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
