using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using backend.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Xunit;

namespace IntegrationTests.Quiz;

public class StudentQuizEndpointTests : IClassFixture<StudentQuizEndpointWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly StudentQuizEndpointWebApplicationFactory _factory;

    public StudentQuizEndpointTests(StudentQuizEndpointWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "BE-IT-SQ-15 — First submission persists quiz_attempts.xp_earned with graded XP")]
    public async Task SubmitAttempt_FirstAttempt_PersistsXpEarnedInQuizAttempts()
    {
        var tag = BuildTag("sq15");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: false);
            var result = await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var persisted = await GetAttemptRowAsync(db, result.AttemptId);
            persisted.XpEarned.Should().Be(60);
            result.XpEarned.Should().Be(60);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-16 — First submission increments Users.XpTotal by earned XP")]
    public async Task SubmitAttempt_FirstAttempt_UpdatesUserXpTotal()
    {
        var tag = BuildTag("sq16");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 15, passScore: 70, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: false);
            var result = await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var updatedXp = await GetUserXpTotalAsync(db, scenario.UserId);
            updatedXp.Should().Be(15 + result.XpEarned);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-17 — Retry submission persists quiz_attempts.xp_earned as 0")]
    public async Task SubmitAttempt_Retry_PersistsZeroXpEarnedInQuizAttempts()
    {
        var tag = BuildTag("sq17");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: false);

            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);
            var retry = await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var persistedRetry = await GetAttemptRowAsync(db, retry.AttemptId);
            retry.IsFirstAttempt.Should().BeFalse();
            retry.XpEarned.Should().Be(0);
            persistedRetry.XpEarned.Should().Be(0);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-18 — Retry submission does not increase Users.XpTotal")]
    public async Task SubmitAttempt_Retry_DoesNotIncreaseUserXpTotal()
    {
        var tag = BuildTag("sq18");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 10, passScore: 70, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: false);

            var first = await SubmitAttemptAsync(scenario, ["A", "A", "A"]);
            var xpAfterFirst = await GetUserXpTotalAsync(db, scenario.UserId);
            xpAfterFirst.Should().Be(10 + first.XpEarned);

            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);
            var xpAfterRetry = await GetUserXpTotalAsync(db, scenario.UserId);
            xpAfterRetry.Should().Be(xpAfterFirst);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-19 — XP threshold badge is inserted into user_badges after submission")]
    public async Task SubmitAttempt_CrossesXpThreshold_AwardsXpBadgeInUserBadges()
    {
        var tag = BuildTag("sq19");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [20, 20, 20], createXpBadges: true, createAlgorithmBadge: false);
            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var awarded = await GetUserBadgeNamesAsync(db, scenario.UserId);
            awarded.Should().Contain(scenario.XpBadge50Name!);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-20 — XP badge is not inserted into user_badges below threshold")]
    public async Task SubmitAttempt_BelowXpThreshold_DoesNotAwardXpBadge()
    {
        var tag = BuildTag("sq20");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 100, rewards: [10, 10, 10], createXpBadges: true, createAlgorithmBadge: false);
            await SubmitAttemptAsync(scenario, ["A", "B", "B"]); // 1 correct => 10 XP

            var awarded = await GetUserBadgeNamesAsync(db, scenario.UserId);
            awarded.Should().NotContain(scenario.XpBadge50Name!);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-21 — Passed quiz inserts algorithm badge into user_badges")]
    public async Task SubmitAttempt_PassedQuiz_AwardsAlgorithmBadge()
    {
        var tag = BuildTag("sq21");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: true);
            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var awarded = await GetUserBadgeNamesAsync(db, scenario.UserId);
            awarded.Should().Contain(scenario.AlgorithmBadgeName!);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-22 — Failed quiz does not insert algorithm badge into user_badges")]
    public async Task SubmitAttempt_FailedQuiz_DoesNotAwardAlgorithmBadge()
    {
        var tag = BuildTag("sq22");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 100, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: true);
            await SubmitAttemptAsync(scenario, ["A", "A", "B"]); // 2/3 => fail at 100%

            var awarded = await GetUserBadgeNamesAsync(db, scenario.UserId);
            awarded.Should().NotContain(scenario.AlgorithmBadgeName!);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-23 — Crossing multiple thresholds inserts all expected XP badges")]
    public async Task SubmitAttempt_CrossesMultipleThresholds_AwardsAllApplicableXpBadges()
    {
        var tag = BuildTag("sq23");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [60, 60, 60], createXpBadges: true, createAlgorithmBadge: false);
            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var awarded = await GetUserBadgeNamesAsync(db, scenario.UserId);
            awarded.Should().Contain(scenario.XpBadge50Name!);
            awarded.Should().Contain(scenario.XpBadge120Name!);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-24 — Retry does not create duplicate rows in user_badges")]
    public async Task SubmitAttempt_Retry_DoesNotDuplicateUserBadges()
    {
        var tag = BuildTag("sq24");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [20, 20, 20], createXpBadges: true, createAlgorithmBadge: true);

            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);
            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            (await GetUserBadgeCountAsync(db, scenario.UserId, scenario.XpBadge50Name!)).Should().Be(1);
            (await GetUserBadgeCountAsync(db, scenario.UserId, scenario.AlgorithmBadgeName!)).Should().Be(1);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-25 — quiz_attempts row stores graded correctness and total_questions for submission")]
    public async Task SubmitAttempt_PersistsScoreAndQuestionCountCorrectly()
    {
        var tag = BuildTag("sq25");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: false);
            var result = await SubmitAttemptAsync(scenario, ["A", "B", "A"]); // 2 correct

            var persisted = await GetAttemptRowAsync(db, result.AttemptId);
            persisted.Score.Should().Be(result.CorrectCount);
            persisted.TotalQuestions.Should().Be(result.TotalQuestions);
            persisted.Passed.Should().Be(result.Passed);
            persisted.XpEarned.Should().Be(result.XpEarned);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-26 — API result XP remains consistent with persisted quiz_attempts and Users.XpTotal")]
    public async Task SubmitAttempt_ResponseXp_IsConsistentWithDatabaseState()
    {
        var tag = BuildTag("sq26");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 25, passScore: 70, rewards: [20, 20, 20], createXpBadges: false, createAlgorithmBadge: false);
            var result = await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var persisted = await GetAttemptRowAsync(db, result.AttemptId);
            var userXp = await GetUserXpTotalAsync(db, scenario.UserId);

            result.XpEarned.Should().Be(persisted.XpEarned);
            userXp.Should().Be(25 + result.XpEarned);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    [Fact(DisplayName = "BE-IT-SQ-27 — Submission awards the correct XP and algorithm badges in user_badges")]
    public async Task SubmitAttempt_AwardsExpectedBadgesInUserBadges()
    {
        var tag = BuildTag("sq27");
        await using var db = await OpenConnectionAsync();

        try
        {
            var scenario = await SeedScenarioAsync(db, tag, initialXp: 0, passScore: 70, rewards: [20, 20, 20], createXpBadges: true, createAlgorithmBadge: true);
            await SubmitAttemptAsync(scenario, ["A", "A", "A"]);

            var awarded = await GetUserBadgeNamesAsync(db, scenario.UserId);
            awarded.Should().Contain(scenario.XpBadge50Name!);
            awarded.Should().Contain(scenario.AlgorithmBadgeName!);
        }
        finally
        {
            await CleanupScenarioAsync(db, tag);
        }
    }

    private async Task<ScenarioContext> SeedScenarioAsync(
        MySqlConnection db,
        string tag,
        int initialXp,
        int passScore,
        int[] rewards,
        bool createXpBadges,
        bool createAlgorithmBadge)
    {
        var algorithmId = await EnsureAnyAlgorithmAsync(db, tag);

        var adminUserId = await InsertUserAsync(
            db,
            clerkUserId: $"{tag}_admin_clerk",
            email: $"{tag}.admin@example.com",
            role: "Admin",
            xpTotal: 0);

        var studentClerkSub = $"{tag}_student_clerk";
        var studentUserId = await InsertUserAsync(
            db,
            clerkUserId: studentClerkSub,
            email: $"{tag}.student@example.com",
            role: "User",
            xpTotal: initialXp);

        var quizId = await InsertQuizAsync(db, tag, algorithmId, adminUserId, passScore);

        foreach (var (reward, idx) in rewards.Select((reward, idx) => (reward, idx)))
        {
            await InsertQuestionAsync(db, quizId, idx + 1, reward, correctOption: "A");
        }

        string? xpBadge50Name = null;
        string? xpBadge120Name = null;
        string? algorithmBadgeName = null;

        if (createXpBadges)
        {
            xpBadge50Name = $"{tag}-XP-50";
            xpBadge120Name = $"{tag}-XP-120";

            await InsertXpBadgeAsync(db, xpBadge50Name, threshold: 50);
            await InsertXpBadgeAsync(db, xpBadge120Name, threshold: 120);
        }

        if (createAlgorithmBadge)
        {
            algorithmBadgeName = $"{tag}-ALGO-BADGE";
            await InsertAlgorithmBadgeAsync(db, algorithmBadgeName, algorithmId);
        }

        return new ScenarioContext
        {
            Tag = tag,
            UserId = studentUserId,
            ClerkSub = studentClerkSub,
            QuizId = quizId,
            InitialXp = initialXp,
            XpBadge50Name = xpBadge50Name,
            XpBadge120Name = xpBadge120Name,
            AlgorithmBadgeName = algorithmBadgeName
        };
    }

    private async Task<SubmitAttemptResult> SubmitAttemptAsync(ScenarioContext scenario, string[] selectedOptions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", StudentQuizTestAuthHandler.Token);
        client.DefaultRequestHeaders.Remove("X-Test-Sub");
        client.DefaultRequestHeaders.Add("X-Test-Sub", scenario.ClerkSub);

        var payload = new
        {
            answers = selectedOptions
                .Select((opt, idx) => new
                {
                    questionId = idx + 1, // remapped below after question lookup
                    selectedOption = opt
                })
                .ToList()
        };

        // Resolve real question IDs from DB order for deterministic payload mapping.
        await using var db = await OpenConnectionAsync();
        var questionIds = await GetQuizQuestionIdsAsync(db, scenario.QuizId);
        for (var i = 0; i < payload.answers.Count; i++)
        {
            payload.answers[i] = new
            {
                questionId = questionIds[i],
                selectedOption = payload.answers[i].selectedOption
            };
        }

        var response = await client.PostAsJsonAsync($"/api/student/quizzes/{scenario.QuizId}/attempt", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SubmitEnvelope>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be("success");
        body.Data.Should().NotBeNull();

        return body.Data!;
    }

    private async Task<List<int>> GetQuizQuestionIdsAsync(MySqlConnection db, int quizId)
    {
        const string sql = @"
            SELECT question_id
            FROM quiz_questions
            WHERE quiz_id = @QuizId
            ORDER BY order_index ASC, question_id ASC;";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@QuizId", quizId);

        var ids = new List<int>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetInt32("question_id"));

        return ids;
    }

    private async Task<AttemptRow> GetAttemptRowAsync(MySqlConnection db, int attemptId)
    {
        const string sql = @"
            SELECT attempt_id, user_id, quiz_id, score, total_questions, xp_earned, passed
            FROM quiz_attempts
            WHERE attempt_id = @AttemptId
            LIMIT 1;";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@AttemptId", attemptId);

        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
        var found = await reader.ReadAsync();
        found.Should().BeTrue($"attempt {attemptId} should exist in quiz_attempts");

        return new AttemptRow
        {
            AttemptId = reader.GetInt32("attempt_id"),
            UserId = reader.GetInt32("user_id"),
            QuizId = reader.GetInt32("quiz_id"),
            Score = reader.GetInt32("score"),
            TotalQuestions = reader.GetInt32("total_questions"),
            XpEarned = reader.GetInt32("xp_earned"),
            Passed = reader.GetBoolean("passed")
        };
    }

    private async Task<int> GetUserXpTotalAsync(MySqlConnection db, int userId)
    {
        const string sql = "SELECT XpTotal FROM Users WHERE Id = @UserId LIMIT 1;";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@UserId", userId);
        var value = await cmd.ExecuteScalarAsync();

        value.Should().NotBeNull();
        return Convert.ToInt32(value);
    }

    private async Task<List<string>> GetUserBadgeNamesAsync(MySqlConnection db, int userId)
    {
        const string sql = @"
            SELECT b.badge_name
            FROM user_badges ub
            INNER JOIN badges b ON b.badge_id = ub.badge_id
            WHERE ub.user_id = @UserId
            ORDER BY b.badge_id ASC;";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var names = new List<string>();
        await using var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString("badge_name"));

        return names;
    }

    private async Task<int> GetUserBadgeCountAsync(MySqlConnection db, int userId, string badgeName)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM user_badges ub
            INNER JOIN badges b ON b.badge_id = ub.badge_id
            WHERE ub.user_id = @UserId
              AND b.badge_name = @BadgeName;";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@BadgeName", badgeName);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<int> EnsureAnyAlgorithmAsync(MySqlConnection db, string tag)
    {
        const string selectSql = @"
            SELECT algorithm_id
            FROM algorithms
            ORDER BY algorithm_id ASC
            LIMIT 1;";

        await using (var selectCmd = new MySqlCommand(selectSql, db))
        {
            var existing = await selectCmd.ExecuteScalarAsync();
            if (existing is not null && existing != DBNull.Value)
                return Convert.ToInt32(existing);
        }

        const string insertSql = @"
            INSERT INTO algorithms
                (name, category, description, time_complexity_best, time_complexity_average, time_complexity_worst, created_at)
            VALUES
                (@Name, 'Sorting', 'Integration-test algorithm', 'O(n)', 'O(n)', 'O(n)', UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var insertCmd = new MySqlCommand(insertSql, db);
        insertCmd.Parameters.AddWithValue("@Name", $"{tag}-algorithm");
        return Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
    }

    private static async Task<int> InsertUserAsync(MySqlConnection db, string clerkUserId, string email, string role, int xpTotal)
    {
        const string sql = @"
            INSERT INTO Users (ClerkUserId, Email, Role, XpTotal, IsActive, CreatedAt)
            VALUES (@ClerkUserId, @Email, @Role, @XpTotal, 1, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@ClerkUserId", clerkUserId);
        cmd.Parameters.AddWithValue("@Email", email);
        cmd.Parameters.AddWithValue("@Role", role);
        cmd.Parameters.AddWithValue("@XpTotal", xpTotal);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<int> InsertQuizAsync(MySqlConnection db, string tag, int algorithmId, int adminUserId, int passScore)
    {
        const string sql = @"
            INSERT INTO quizzes
                (algorithm_id, created_by, title, description, time_limit_mins, pass_score, is_active, created_at, updated_at)
            VALUES
                (@AlgorithmId, @CreatedBy, @Title, 'Integration test quiz', NULL, @PassScore, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@AlgorithmId", algorithmId);
        cmd.Parameters.AddWithValue("@CreatedBy", adminUserId);
        cmd.Parameters.AddWithValue("@Title", $"{tag}-quiz");
        cmd.Parameters.AddWithValue("@PassScore", passScore);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<int> InsertQuestionAsync(MySqlConnection db, int quizId, int orderIndex, int xpReward, string correctOption)
    {
        const string sql = @"
            INSERT INTO quiz_questions
                (quiz_id, question_type, question_text, option_a, option_b, option_c, option_d,
                 correct_option, difficulty, explanation, order_index, is_active, created_at, xp_reward)
            VALUES
                (@QuizId, 'MCQ', @QuestionText, 'Option A', 'Option B', 'Option C', 'Option D',
                 @CorrectOption, 'easy', 'Test explanation', @OrderIndex, 1, UTC_TIMESTAMP(), @XpReward);
            SELECT LAST_INSERT_ID();";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@QuizId", quizId);
        cmd.Parameters.AddWithValue("@QuestionText", $"Question {orderIndex}");
        cmd.Parameters.AddWithValue("@CorrectOption", correctOption);
        cmd.Parameters.AddWithValue("@OrderIndex", orderIndex);
        cmd.Parameters.AddWithValue("@XpReward", xpReward);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task InsertXpBadgeAsync(MySqlConnection db, string badgeName, int threshold)
    {
        const string sql = @"
            INSERT INTO badges
                (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id, created_at)
            VALUES
                (@BadgeName, 'Integration test XP badge', @Threshold, 'star', '#00AA66', 'Earn test XP', NULL, UTC_TIMESTAMP());";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@BadgeName", badgeName);
        cmd.Parameters.AddWithValue("@Threshold", threshold);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertAlgorithmBadgeAsync(MySqlConnection db, string badgeName, int algorithmId)
    {
        const string sql = @"
            INSERT INTO badges
                (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint, algorithm_id, created_at)
            VALUES
                (@BadgeName, 'Integration test algorithm badge', 0, 'trophy', '#0044FF', 'Pass algorithm quiz', @AlgorithmId, UTC_TIMESTAMP());";

        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@BadgeName", badgeName);
        cmd.Parameters.AddWithValue("@AlgorithmId", algorithmId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CleanupScenarioAsync(MySqlConnection db, string tag)
    {
        var prefix = $"{tag}%";

        const string deleteUserBadgesSql = @"
            DELETE ub
            FROM user_badges ub
            LEFT JOIN Users u ON u.Id = ub.user_id
            LEFT JOIN badges b ON b.badge_id = ub.badge_id
            WHERE u.email LIKE @Prefix
               OR b.badge_name LIKE @Prefix;";

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

        const string deleteQuestionsSql = @"
            DELETE qq
            FROM quiz_questions qq
            INNER JOIN quizzes q ON q.quiz_id = qq.quiz_id
            WHERE q.title LIKE @Prefix;";

        const string deleteQuizzesSql = "DELETE FROM quizzes WHERE title LIKE @Prefix;";
        const string deleteBadgesSql = "DELETE FROM badges WHERE badge_name LIKE @Prefix;";
        const string deleteUsersSql = "DELETE FROM Users WHERE email LIKE @Prefix;";

        foreach (var sql in new[]
                 {
                     deleteUserBadgesSql,
                     deleteAttemptAnswersSql,
                     deleteAttemptsSql,
                     deleteQuestionsSql,
                     deleteQuizzesSql,
                     deleteBadgesSql,
                     deleteUsersSql
                 })
        {
            await using var cmd = new MySqlCommand(sql, db);
            cmd.Parameters.AddWithValue("@Prefix", prefix);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<MySqlConnection> OpenConnectionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseHelper>();
        return await db.OpenConnectionAsync();
    }

    private static string BuildTag(string id)
        => $"it_{id}_{Guid.NewGuid():N}";

    private sealed class ScenarioContext
    {
        public required string Tag { get; init; }
        public required int UserId { get; init; }
        public required string ClerkSub { get; init; }
        public required int QuizId { get; init; }
        public required int InitialXp { get; init; }
        public string? XpBadge50Name { get; init; }
        public string? XpBadge120Name { get; init; }
        public string? AlgorithmBadgeName { get; init; }
    }

    private sealed class SubmitEnvelope
    {
        public string Status { get; set; } = string.Empty;
        public SubmitAttemptResult? Data { get; set; }
    }

    private sealed class SubmitAttemptResult
    {
        public int AttemptId { get; set; }
        public int QuizId { get; set; }
        public int Score { get; set; }
        public int CorrectCount { get; set; }
        public int TotalQuestions { get; set; }
        public int XpEarned { get; set; }
        public bool Passed { get; set; }
        public bool IsFirstAttempt { get; set; }
    }

    private sealed class AttemptRow
    {
        public int AttemptId { get; init; }
        public int UserId { get; init; }
        public int QuizId { get; init; }
        public int Score { get; init; }
        public int TotalQuestions { get; init; }
        public int XpEarned { get; init; }
        public bool Passed { get; init; }
    }
}

public sealed class StudentQuizEndpointWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = StudentQuizTestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = StudentQuizTestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, StudentQuizTestAuthHandler>(
                StudentQuizTestAuthHandler.SchemeName, _ => { });
        });
    }
}

public sealed class StudentQuizTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "StudentQuizTestAuth";
    public const string Token = "student-quiz-test-token";

    public StudentQuizTestAuthHandler(
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
        if (!string.Equals(token, Token, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Token is invalid."));

        var sub = Request.Headers.TryGetValue("X-Test-Sub", out var subs)
            ? subs.ToString()
            : "clerk_student_quiz_test_default";

        if (string.IsNullOrWhiteSpace(sub))
            sub = "clerk_student_quiz_test_default";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub),
            new("sub", sub),
            new(ClaimTypes.Name, "student-quiz-test-user"),
            new(ClaimTypes.Email, "student.quiz.test@example.com"),
            new(ClaimTypes.Role, "User")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}