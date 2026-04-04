using System.Security.Claims;
using backend.Controllers;
using backend.DTOs;
using backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="StudentQuizController"/>.
///
/// Scenarios covered
/// -----------------
/// GetActiveQuizzes
///   1. Returns 200 OK with list of active quizzes
/// GetActiveQuizById
///   2. Returns 200 OK when active quiz found
///   3. Returns 404 Not Found when quiz inactive or missing
/// GetActiveQuestions
///   4. Returns 200 OK with student-safe questions (no correct answers)
///   5. Returns 404 Not Found when quiz inactive or not found
/// SubmitAttempt
///   6. Returns 200 OK with graded result on success
///   7. Returns 401 Unauthorized when JWT has no user identifier
///   8. Returns 400 Bad Request when service throws ArgumentException (invalid submission)
///   9. Returns 404 Not Found when service throws KeyNotFoundException (quiz or user missing)
/// </summary>
public class StudentQuizControllerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static QuizResponseDto SampleQuizDto(int id = 1) => new()
    {
        QuizId      = id,
        Title       = "Active Quiz",
        IsActive    = true,
        PassScore   = 70,
        AlgorithmId = 1,
        CreatedBy   = 5,
        CreatedAt   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt   = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static StudentQuizQuestionResponseDto SampleStudentQuestion(int id = 1) => new()
    {
        QuestionId   = id,
        QuestionType = "MCQ",
        QuestionText = "What is O(n)?",
        OptionA      = "Constant",
        OptionB      = "Linear",
        OptionC      = "Quadratic",
        OptionD      = "Logarithmic",
        Difficulty   = "easy",
        OrderIndex   = 0
    };

    private static QuizAttemptResultDto SampleAttemptResult() => new()
    {
        AttemptId      = 99,
        QuizId         = 1,
        Score          = 100,
        CorrectCount   = 1,
        TotalQuestions = 1,
        XpEarned       = 10,
        Passed         = true,
        IsFirstAttempt = true,
        Results        = new List<QuizAttemptAnswerResultDto>
        {
            new()
            {
                QuestionId     = 1,
                SelectedOption = "B",
                CorrectOption  = "B",
                IsCorrect      = true
            }
        }
    };

    private static StudentQuizController BuildController(
        Mock<IQuizService>         quizSvc,
        Mock<IQuizQuestionService> questionSvc,
        Mock<IQuizAttemptService>  attemptSvc,
        string? clerkUserId = "clerk_001")
    {
        var ctrl = new StudentQuizController(
            quizSvc.Object,
            questionSvc.Object,
            attemptSvc.Object,
            NullLogger<StudentQuizController>.Instance);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = clerkUserId is null
                    ? new ClaimsPrincipal(new ClaimsIdentity())
                    : new ClaimsPrincipal(new ClaimsIdentity(new[]
                      {
                          new Claim(ClaimTypes.NameIdentifier, clerkUserId)
                      }, "TestAuth"))
            }
        };

        return ctrl;
    }

    // -----------------------------------------------------------------------
    // GetActiveQuizzes
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — GetActiveQuizzes: returns 200 OK with active quiz list")]
    public async Task Should_Return200_When_GetActiveQuizzesCalled()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        quizSvc.Setup(s => s.GetActiveQuizzesAsync())
               .ReturnsAsync(new[] { SampleQuizDto(1), SampleQuizDto(2) });

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .GetActiveQuizzes() as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);

        var data = result.Value!.GetType().GetProperty("data")!.GetValue(result.Value)
                   as IEnumerable<QuizResponseDto>;
        data!.Should().HaveCount(2);
    }

    // -----------------------------------------------------------------------
    // GetActiveQuizById
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — GetActiveQuizById: returns 200 OK when active quiz found")]
    public async Task Should_Return200_When_ActiveQuizFound()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        quizSvc.Setup(s => s.GetActiveQuizByIdAsync(1)).ReturnsAsync(SampleQuizDto(1));

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .GetActiveQuizById(1) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "Scenario 3 — GetActiveQuizById: returns 404 when quiz inactive or missing")]
    public async Task Should_Return404_When_QuizInactiveOrMissing()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        quizSvc.Setup(s => s.GetActiveQuizByIdAsync(99)).ReturnsAsync((QuizResponseDto?)null);

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .GetActiveQuizById(99) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // GetActiveQuestions
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 4 — GetActiveQuestions: returns 200 OK with student-safe questions")]
    public async Task Should_Return200_When_StudentQuestionsRetrieved()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        questionSvc.Setup(s => s.GetActiveQuestionsForStudentAsync(1))
                   .ReturnsAsync(new[] { SampleStudentQuestion(1), SampleStudentQuestion(2) });

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .GetActiveQuestions(1) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);

        var data = result.Value!.GetType().GetProperty("data")!.GetValue(result.Value)
                   as IEnumerable<StudentQuizQuestionResponseDto>;
        data!.Should().HaveCount(2);
        // Verify student DTO has no CorrectOption
        data.First().GetType().GetProperty("CorrectOption").Should().BeNull();
    }

    [Fact(DisplayName = "Scenario 5 — GetActiveQuestions: returns 404 when quiz inactive or not found")]
    public async Task Should_Return404_When_QuizInactiveForStudentQuestions()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        questionSvc.Setup(s => s.GetActiveQuestionsForStudentAsync(99))
                   .ThrowsAsync(new KeyNotFoundException("Quiz with ID 99 does not exist."));

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .GetActiveQuestions(99) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // SubmitAttempt
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 6 — SubmitAttempt: returns 200 OK with graded result on success")]
    public async Task Should_Return200_When_AttemptSubmittedSuccessfully()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        attemptSvc.Setup(s => s.SubmitAttemptAsync(1, "clerk_001", It.IsAny<CreateQuizAttemptDto>()))
                  .ReturnsAsync(SampleAttemptResult());

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "B" }
            }
        };

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .SubmitAttempt(1, dto) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);

        var status = result.Value!.GetType().GetProperty("status")!.GetValue(result.Value) as string;
        status.Should().Be("success");
    }

    [Fact(DisplayName = "Scenario 7 — SubmitAttempt: returns 401 when JWT has no user identifier")]
    public async Task Should_Return401_When_JwtMissingUserIdentifier()
    {
        // Arrange — controller with no claims
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" }
            }
        };

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc, clerkUserId: null)
                         .SubmitAttempt(1, dto) as UnauthorizedObjectResult;

        // Assert
        result!.StatusCode.Should().Be(401);
    }

    [Fact(DisplayName = "Scenario 8 — SubmitAttempt: returns 400 Bad Request on invalid submission")]
    public async Task Should_Return400_When_SubmissionIsInvalid()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        attemptSvc.Setup(s => s.SubmitAttemptAsync(1, "clerk_001", It.IsAny<CreateQuizAttemptDto>()))
                  .ThrowsAsync(new ArgumentException("Exactly 3 answers are required for quiz 1."));

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" }   // only 1 of 3 required
            }
        };

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .SubmitAttempt(1, dto) as BadRequestObjectResult;

        // Assert
        result!.StatusCode.Should().Be(400);

        var message = result.Value!.GetType().GetProperty("message")!.GetValue(result.Value) as string;
        message.Should().Contain("3 answers");
    }

    [Fact(DisplayName = "Scenario 9 — SubmitAttempt: returns 404 when quiz or user not found")]
    public async Task Should_Return404_When_QuizOrUserNotFoundOnSubmit()
    {
        // Arrange
        var quizSvc     = new Mock<IQuizService>();
        var questionSvc = new Mock<IQuizQuestionService>();
        var attemptSvc  = new Mock<IQuizAttemptService>();

        attemptSvc.Setup(s => s.SubmitAttemptAsync(99, "clerk_001", It.IsAny<CreateQuizAttemptDto>()))
                  .ThrowsAsync(new KeyNotFoundException("Quiz with ID 99 does not exist."));

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" }
            }
        };

        // Act
        var result = await BuildController(quizSvc, questionSvc, attemptSvc)
                         .SubmitAttempt(99, dto) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }
}
