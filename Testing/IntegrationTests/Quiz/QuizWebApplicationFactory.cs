using backend.DTOs;
using backend.Services;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationTests.Quiz;

// =============================================================================
// Quiz-specific WebApplicationFactory
// =============================================================================
// Cannot inherit CustomWebApplicationFactory (sealed). Instead this factory
// mirrors all of its setup (TestAuthHandler, StubUserService, test config)
// and additionally stubs the three quiz services — no database is required.
// =============================================================================

public sealed class QuizWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        // Same test config overrides as CustomWebApplicationFactory
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clerk:Authority"]                     = "https://test.clerk.example.com",
                ["Clerk:SecretKey"]                     = "sk_test_dummy_value_for_integration_tests",
                ["ConnectionStrings:DefaultConnection"]  =
                    "Server=localhost;Database=test_db;User=test;Password=test;"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace Clerk JWT with the deterministic test scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme    = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            // Stub user service (no DB)
            services.RemoveAll<IUserService>();
            services.AddScoped<IUserService, StubUserService>();

            // Stub all three quiz services (no DB)
            services.RemoveAll<IQuizService>();
            services.AddScoped<IQuizService, StubQuizService>();

            services.RemoveAll<IQuizQuestionService>();
            services.AddScoped<IQuizQuestionService, StubQuizQuestionService>();

            services.RemoveAll<IQuizAttemptService>();
            services.AddScoped<IQuizAttemptService, StubQuizAttemptService>();
        });
    }
}

// =============================================================================
// Stub: IQuizService
// =============================================================================
// • quizId = 1  → success responses (found, updated, deleted)
// • quizId ≠ 1  → null (not found) / false (delete) / KeyNotFoundException (update)
// • CreateQuizAsync always succeeds (clerk sub from TestAuthHandler = "clerk_test_001")
// =============================================================================

public class StubQuizService : IQuizService
{
    internal static QuizResponseDto Sample(int id = 1) => new()
    {
        QuizId        = id,
        AlgorithmId   = 1,
        CreatedBy     = 5,
        Title         = "Integration Test Quiz",
        Description   = "Sample quiz for integration tests",
        TimeLimitMins = 30,
        PassScore     = 70,
        IsActive      = true,
        CreatedAt     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt     = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    public virtual Task<IEnumerable<QuizResponseDto>> GetAllQuizzesAsync() =>
        Task.FromResult<IEnumerable<QuizResponseDto>>(new[] { Sample(1), Sample(2) });

    public virtual Task<IEnumerable<QuizResponseDto>> GetActiveQuizzesAsync() =>
        Task.FromResult<IEnumerable<QuizResponseDto>>(new[] { Sample(1) });

    public virtual Task<QuizResponseDto?> GetQuizByIdAsync(int id) =>
        Task.FromResult<QuizResponseDto?>(id == 1 ? Sample(id) : null);

    public virtual Task<QuizResponseDto?> GetActiveQuizByIdAsync(int id) =>
        Task.FromResult<QuizResponseDto?>(id == 1 ? Sample(id) : null);

    public virtual Task<QuizResponseDto> CreateQuizAsync(CreateQuizDto dto, string clerkUserId)
    {
        var created = Sample(1);
        created.Title = dto.Title;
        return Task.FromResult(created);
    }

    public virtual Task<QuizResponseDto> UpdateQuizAsync(int id, UpdateQuizDto dto)
    {
        if (id != 1)
            throw new KeyNotFoundException($"Quiz with ID {id} was not found.");

        var updated = Sample(id);
        updated.Title     = dto.Title;
        updated.IsActive  = dto.IsActive;
        updated.PassScore = dto.PassScore;
        return Task.FromResult(updated);
    }

    public virtual Task<bool> DeleteQuizAsync(int id) =>
        Task.FromResult(id == 1);
}

// Variant: CreateQuizAsync throws KeyNotFoundException (user not synced)
public sealed class UserNotSyncedQuizService : StubQuizService
{
    public override Task<QuizResponseDto> CreateQuizAsync(CreateQuizDto dto, string clerkUserId) =>
        throw new KeyNotFoundException(
            "Authenticated user does not have a local account. Complete user sync first.");
}

// Variant: CreateQuizAsync throws ArgumentException (algorithm not found)
public sealed class AlgorithmNotFoundQuizService : StubQuizService
{
    public override Task<QuizResponseDto> CreateQuizAsync(CreateQuizDto dto, string clerkUserId) =>
        throw new ArgumentException($"Algorithm with ID {dto.AlgorithmId} does not exist.");
}

// =============================================================================
// Stub: IQuizQuestionService
// =============================================================================
// • questionId = 1, quizId = 1  → success
// • questionId ≠ 1              → null / KeyNotFoundException
// • quizId ≠ 1 for list ops     → KeyNotFoundException
// =============================================================================

public sealed class StubQuizQuestionService : IQuizQuestionService
{
    internal static QuizQuestionResponseDto SampleAdmin(int id = 1, int quizId = 1) => new()
    {
        QuestionId    = id,
        QuizId        = quizId,
        QuestionType  = "MCQ",
        QuestionText  = "What is the time complexity of O(n)?",
        OptionA       = "Constant",
        OptionB       = "Linear",
        OptionC       = "Quadratic",
        OptionD       = "Logarithmic",
        CorrectOption = "B",
        Difficulty    = "easy",
        IsActive      = true,
        OrderIndex    = 0,
        CreatedAt     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    internal static StudentQuizQuestionResponseDto SampleStudent(int id = 1) => new()
    {
        QuestionId   = id,
        QuestionType = "MCQ",
        QuestionText = "What is the time complexity of O(n)?",
        OptionA      = "Constant",
        OptionB      = "Linear",
        OptionC      = "Quadratic",
        OptionD      = "Logarithmic",
        Difficulty   = "easy",
        OrderIndex   = 0
    };

    public Task<IEnumerable<QuizQuestionResponseDto>> GetByQuizIdAsync(int quizId)
    {
        if (quizId != 1)
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");

        return Task.FromResult<IEnumerable<QuizQuestionResponseDto>>(
            new[] { SampleAdmin(1, quizId) });
    }

    public Task<IEnumerable<StudentQuizQuestionResponseDto>> GetActiveQuestionsForStudentAsync(int quizId)
    {
        if (quizId != 1)
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");

        return Task.FromResult<IEnumerable<StudentQuizQuestionResponseDto>>(
            new[] { SampleStudent(1) });
    }

    public Task<QuizQuestionResponseDto?> GetByIdAsync(int questionId) =>
        Task.FromResult<QuizQuestionResponseDto?>(
            questionId == 1 ? SampleAdmin(questionId, quizId: 1) : null);

    public Task<QuizQuestionResponseDto> CreateAsync(int quizId, CreateQuizQuestionDto dto)
    {
        if (quizId != 1)
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");

        var created = SampleAdmin(id: 10, quizId: quizId);
        created.QuestionText = dto.QuestionText;
        return Task.FromResult(created);
    }

    public Task<QuizQuestionResponseDto> UpdateAsync(int questionId, UpdateQuizQuestionDto dto)
    {
        if (questionId != 1)
            throw new KeyNotFoundException($"Question with ID {questionId} was not found.");

        var updated = SampleAdmin(questionId);
        updated.QuestionText = dto.QuestionText;
        return Task.FromResult(updated);
    }

    public Task<bool> DeleteAsync(int questionId) =>
        Task.FromResult(questionId == 1);
}

// =============================================================================
// Stub: IQuizAttemptService
// =============================================================================
// • quizId = 1 + valid answers  → success result
// • quizId ≠ 1                  → KeyNotFoundException (quiz not found)
// =============================================================================

public sealed class StubQuizAttemptService : IQuizAttemptService
{
    public Task<QuizAttemptResultDto> SubmitAttemptAsync(
        int quizId, string clerkUserId, CreateQuizAttemptDto dto)
    {
        if (quizId != 1)
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");

        var result = new QuizAttemptResultDto
        {
            AttemptId      = 42,
            QuizId         = quizId,
            Score          = 100,
            CorrectCount   = dto.Answers.Count,
            TotalQuestions = dto.Answers.Count,
            XpEarned       = dto.Answers.Count * 10,
            Passed         = true,
            IsFirstAttempt = true,
            Results        = dto.Answers.Select(a => new QuizAttemptAnswerResultDto
            {
                QuestionId     = a.QuestionId,
                SelectedOption = a.SelectedOption,
                CorrectOption  = a.SelectedOption,
                IsCorrect      = true
            }).ToList()
        };

        return Task.FromResult(result);
    }
}

// Variant: always throws ArgumentException (wrong answer count business rule)
public sealed class InvalidAnswersAttemptService : IQuizAttemptService
{
    public Task<QuizAttemptResultDto> SubmitAttemptAsync(
        int quizId, string clerkUserId, CreateQuizAttemptDto dto) =>
        throw new ArgumentException($"Exactly 3 answers are required for quiz {quizId}.");
}
