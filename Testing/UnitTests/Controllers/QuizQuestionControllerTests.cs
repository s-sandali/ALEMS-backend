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
/// Unit tests for <see cref="QuizQuestionController"/>.
///
/// Scenarios covered
/// -----------------
/// GetQuestions
///   1. Returns 200 OK with question list
///   2. Returns 404 Not Found when quiz does not exist
/// GetQuestion
///   3. Returns 200 OK when question belongs to quiz
///   4. Returns 404 Not Found when question not found
///   5. Returns 404 Not Found when question belongs to a different quiz
/// CreateQuestion
///   6. Returns 201 Created on success
///   7. Returns 404 Not Found when quiz does not exist
/// UpdateQuestion
///   8. Returns 200 OK on success
///   9. Returns 404 Not Found when question not found in this quiz (pre-check)
///  10. Returns 404 Not Found when service throws KeyNotFoundException during update
/// DeleteQuestion
///  11. Returns 204 No Content on success
///  12. Returns 404 Not Found when question not found in this quiz
/// </summary>
public class QuizQuestionControllerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static QuizQuestionResponseDto SampleDto(int id = 1, int quizId = 1) => new()
    {
        QuestionId    = id,
        QuizId        = quizId,
        QuestionType  = "MCQ",
        QuestionText  = "What is O(n)?",
        OptionA       = "Constant",
        OptionB       = "Linear",
        OptionC       = "Quadratic",
        OptionD       = "Logarithmic",
        CorrectOption = "B",
        Difficulty    = "easy",
        IsActive      = true,
        CreatedAt     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static QuizQuestionController BuildController(Mock<IQuizQuestionService> svc)
    {
        var ctrl = new QuizQuestionController(svc.Object, NullLogger<QuizQuestionController>.Instance);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return ctrl;
    }

    // -----------------------------------------------------------------------
    // GetQuestions
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — GetQuestions: returns 200 OK with question list")]
    public async Task Should_Return200_When_QuestionsFound()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByQuizIdAsync(1))
           .ReturnsAsync(new[] { SampleDto(1), SampleDto(2) });

        // Act
        var result = await BuildController(svc).GetQuestions(1) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);

        var data = result.Value!.GetType().GetProperty("data")!.GetValue(result.Value)
                   as IEnumerable<QuizQuestionResponseDto>;
        data!.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Scenario 2 — GetQuestions: returns 404 Not Found when quiz does not exist")]
    public async Task Should_Return404_When_QuizNotFoundForGetQuestions()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByQuizIdAsync(99))
           .ThrowsAsync(new KeyNotFoundException("Quiz with ID 99 does not exist."));

        // Act
        var result = await BuildController(svc).GetQuestions(99) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // GetQuestion
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — GetQuestion: returns 200 OK when question belongs to quiz")]
    public async Task Should_Return200_When_QuestionBelongsToQuiz()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleDto(id: 1, quizId: 1));

        // Act
        var result = await BuildController(svc).GetQuestion(quizId: 1, id: 1) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "Scenario 4 — GetQuestion: returns 404 Not Found when question not found at all")]
    public async Task Should_Return404_When_QuestionNotFound()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(99)).ReturnsAsync((QuizQuestionResponseDto?)null);

        // Act
        var result = await BuildController(svc).GetQuestion(quizId: 1, id: 99) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    [Fact(DisplayName = "Scenario 5 — GetQuestion: returns 404 Not Found when question belongs to a different quiz")]
    public async Task Should_Return404_When_QuestionBelongsToOtherQuiz()
    {
        // Arrange — question exists but under quizId=2, not quizId=1
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleDto(id: 1, quizId: 2));

        // Act
        var result = await BuildController(svc).GetQuestion(quizId: 1, id: 1) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // CreateQuestion
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 6 — CreateQuestion: returns 201 Created on success")]
    public async Task Should_Return201_When_QuestionCreatedSuccessfully()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.CreateAsync(1, It.IsAny<CreateQuizQuestionDto>()))
           .ReturnsAsync(SampleDto(id: 5, quizId: 1));

        var dto = new CreateQuizQuestionDto
        {
            QuestionType  = "MCQ",
            QuestionText  = "New question?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "A",
            Difficulty    = "easy"
        };

        // Act
        var result = await BuildController(svc).CreateQuestion(1, dto) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(201);

        var status = result.Value!.GetType().GetProperty("status")!.GetValue(result.Value) as string;
        status.Should().Be("success");
    }

    [Fact(DisplayName = "Scenario 7 — CreateQuestion: returns 404 Not Found when quiz does not exist")]
    public async Task Should_Return404_When_QuizNotFoundOnCreateQuestion()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.CreateAsync(99, It.IsAny<CreateQuizQuestionDto>()))
           .ThrowsAsync(new KeyNotFoundException("Quiz with ID 99 does not exist."));

        var dto = new CreateQuizQuestionDto
        {
            QuestionType  = "MCQ", QuestionText = "Q?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "A", Difficulty = "easy"
        };

        // Act
        var result = await BuildController(svc).CreateQuestion(99, dto) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // UpdateQuestion
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 8 — UpdateQuestion: returns 200 OK on successful update")]
    public async Task Should_Return200_When_QuestionUpdatedSuccessfully()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleDto(id: 1, quizId: 1));
        svc.Setup(s => s.UpdateAsync(1, It.IsAny<UpdateQuizQuestionDto>()))
           .ReturnsAsync(SampleDto(id: 1, quizId: 1));

        var dto = new UpdateQuizQuestionDto
        {
            QuestionType  = "MCQ", QuestionText  = "Updated Q?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "B", Difficulty = "medium", IsActive = true
        };

        // Act
        var result = await BuildController(svc).UpdateQuestion(quizId: 1, id: 1, dto) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);

        var status = result.Value!.GetType().GetProperty("status")!.GetValue(result.Value) as string;
        status.Should().Be("success");
    }

    [Fact(DisplayName = "Scenario 9 — UpdateQuestion: returns 404 Not Found when question not in this quiz (pre-check)")]
    public async Task Should_Return404_When_QuestionNotInQuizOnUpdate()
    {
        // Arrange — question exists but belongs to quizId=2
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleDto(id: 1, quizId: 2));

        var dto = new UpdateQuizQuestionDto
        {
            QuestionType = "MCQ", QuestionText = "Q?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "A", Difficulty = "easy", IsActive = true
        };

        // Act
        var result = await BuildController(svc).UpdateQuestion(quizId: 1, id: 1, dto) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
        // Service UpdateAsync should NOT have been called
        svc.Verify(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateQuizQuestionDto>()), Times.Never);
    }

    [Fact(DisplayName = "Scenario 10 — UpdateQuestion: returns 404 when service throws KeyNotFoundException")]
    public async Task Should_Return404_When_ServiceThrowsKeyNotFoundOnUpdate()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleDto(id: 1, quizId: 1));
        svc.Setup(s => s.UpdateAsync(1, It.IsAny<UpdateQuizQuestionDto>()))
           .ThrowsAsync(new KeyNotFoundException("Question with ID 1 was not found."));

        var dto = new UpdateQuizQuestionDto
        {
            QuestionType = "MCQ", QuestionText = "Q?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "A", Difficulty = "easy", IsActive = true
        };

        // Act
        var result = await BuildController(svc).UpdateQuestion(quizId: 1, id: 1, dto) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // DeleteQuestion
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 11 — DeleteQuestion: returns 204 No Content on success")]
    public async Task Should_Return204_When_QuestionDeletedSuccessfully()
    {
        // Arrange
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleDto(id: 1, quizId: 1));
        svc.Setup(s => s.DeleteAsync(1)).ReturnsAsync(true);

        // Act
        var result = await BuildController(svc).DeleteQuestion(quizId: 1, id: 1) as NoContentResult;

        // Assert
        result!.StatusCode.Should().Be(204);
    }

    [Fact(DisplayName = "Scenario 12 — DeleteQuestion: returns 404 Not Found when question not in this quiz")]
    public async Task Should_Return404_When_QuestionNotInQuizOnDelete()
    {
        // Arrange — question belongs to quizId=2
        var svc = new Mock<IQuizQuestionService>();
        svc.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(SampleDto(id: 1, quizId: 2));

        // Act
        var result = await BuildController(svc).DeleteQuestion(quizId: 1, id: 1) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
        // DeleteAsync must NOT be invoked
        svc.Verify(s => s.DeleteAsync(It.IsAny<int>()), Times.Never);
    }
}
