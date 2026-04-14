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
        await using var db = await OpenConnectionAsync();

        try
        {
            var userId = await InsertUserAsync(db, clerkSub, $"{tag}.student@example.com", xpTotal: 130);

            var badge50 = $"{tag}-badge-50";
            var badge120 = $"{tag}-badge-120";
            await InsertXpBadgeAsync(db, badge50, 50);
            await InsertXpBadgeAsync(db, badge120, 120);

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
            earnedBadges.Any(b => string.Equals(b.GetProperty("name").GetString(), badge50, StringComparison.Ordinal)).Should().BeTrue();
            earnedBadges.Any(b => string.Equals(b.GetProperty("name").GetString(), badge120, StringComparison.Ordinal)).Should().BeTrue();

            var allBadges = data.GetProperty("allBadges").EnumerateArray().ToList();
            allBadges.Any(b =>
                string.Equals(b.GetProperty("name").GetString(), badge50, StringComparison.Ordinal) &&
                b.GetProperty("earned").GetBoolean()).Should().BeTrue();
            allBadges.Any(b =>
                string.Equals(b.GetProperty("name").GetString(), badge120, StringComparison.Ordinal) &&
                b.GetProperty("earned").GetBoolean()).Should().BeTrue();
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

    private static async Task<int> InsertUserAsync(MySqlConnection db, string clerkSub, string email, int xpTotal)
    {
        const string sql = @"
            INSERT INTO Users (ClerkUserId, Email, Role, XpTotal, IsActive, CreatedAt)
            VALUES (@ClerkSub, @Email, 'User', @XpTotal, 1, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@ClerkSub", clerkSub);
        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@XpTotal", xpTotal);

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

        foreach (var sql in new[] { deleteUserBadgesSql, deleteUsersSql, deleteBadgesSql })
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
