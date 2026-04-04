using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Business logic for quiz question CRUD operations.
/// </summary>
public class QuizQuestionService : IQuizQuestionService
{
    private readonly IQuizQuestionRepository     _questionRepository;
    private readonly IQuizRepository             _quizRepository;
    private readonly IXpService                  _xpService;
    private readonly ILogger<QuizQuestionService> _logger;

    public QuizQuestionService(
        IQuizQuestionRepository questionRepository,
        IQuizRepository quizRepository,
        IXpService xpService,
        ILogger<QuizQuestionService> logger)
    {
        _questionRepository = questionRepository;
        _quizRepository     = quizRepository;
        _xpService          = xpService;
        _logger             = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuizQuestionResponseDto>> GetByQuizIdAsync(int quizId)
    {
        var quiz = await _quizRepository.GetByIdAsync(quizId);
        if (quiz is null)
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");

        var questions = await _questionRepository.GetByQuizIdAsync(quizId);
        return questions.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StudentQuizQuestionResponseDto>> GetActiveQuestionsForStudentAsync(int quizId)
    {
        // Quiz must exist AND be active — inactive quizzes are invisible to students
        var quiz = await _quizRepository.GetActiveByIdAsync(quizId);
        if (quiz is null)
        {
            _logger.LogWarning(
                "GetActiveQuestionsForStudent: quiz not found or inactive — QuizId={QuizId}", quizId);
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");
        }

        var questions = await _questionRepository.GetByQuizIdAsync(quizId);
        return questions.Select(MapToStudentDto);
    }

    /// <inheritdoc />
    public async Task<QuizQuestionResponseDto?> GetByIdAsync(int questionId)
    {
        var question = await _questionRepository.GetByIdAsync(questionId);
        return question is not null ? MapToDto(question) : null;
    }

    /// <inheritdoc />
    public async Task<QuizQuestionResponseDto> CreateAsync(int quizId, CreateQuizQuestionDto dto)
    {
        var quiz = await _quizRepository.GetByIdAsync(quizId);
        if (quiz is null)
        {
            _logger.LogWarning("CreateQuestion: quiz not found — QuizId={QuizId}", quizId);
            throw new KeyNotFoundException($"Quiz with ID {quizId} does not exist.");
        }

        var question = new QuizQuestion
        {
            QuizId        = quizId,
            QuestionType  = dto.QuestionType,
            QuestionText  = dto.QuestionText,
            OptionA       = dto.OptionA,
            OptionB       = dto.OptionB,
            OptionC       = dto.OptionC,
            OptionD       = dto.OptionD,
            CorrectOption = dto.CorrectOption,
            Difficulty    = dto.Difficulty,
            XpReward      = _xpService.CalculateXP("quiz", dto.Difficulty),
            Explanation   = dto.Explanation,
            OrderIndex    = dto.OrderIndex,
            IsActive      = true
        };

        var created = await _questionRepository.CreateAsync(question);
        _logger.LogInformation(
            "CreateQuestion: QuestionId={QuestionId} added to QuizId={QuizId} (type={Type})",
            created.QuestionId, quizId, dto.QuestionType);

        return MapToDto(created);
    }

    /// <inheritdoc />
    public async Task<QuizQuestionResponseDto> UpdateAsync(int questionId, UpdateQuizQuestionDto dto)
    {
        var existing = await _questionRepository.GetByIdAsync(questionId);
        if (existing is null)
        {
            _logger.LogWarning("UpdateQuestion: question not found — QuestionId={Id}", questionId);
            throw new KeyNotFoundException($"Question with ID {questionId} was not found.");
        }

        existing.QuestionType  = dto.QuestionType;
        existing.QuestionText  = dto.QuestionText;
        existing.OptionA       = dto.OptionA;
        existing.OptionB       = dto.OptionB;
        existing.OptionC       = dto.OptionC;
        existing.OptionD       = dto.OptionD;
        existing.CorrectOption = dto.CorrectOption;
        existing.Difficulty    = dto.Difficulty;
        existing.XpReward      = _xpService.CalculateXP("quiz", dto.Difficulty);
        existing.Explanation   = dto.Explanation;
        existing.OrderIndex    = dto.OrderIndex;
        existing.IsActive      = dto.IsActive;

        await _questionRepository.UpdateAsync(existing);
        _logger.LogInformation("UpdateQuestion: QuestionId={QuestionId} updated", questionId);

        var updated = await _questionRepository.GetByIdAsync(questionId);
        return MapToDto(updated!);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int questionId)
    {
        var success = await _questionRepository.DeleteAsync(questionId);

        if (success)
            _logger.LogInformation("DeleteQuestion: QuestionId={QuestionId} soft-deleted", questionId);
        else
            _logger.LogWarning("DeleteQuestion: question not found — QuestionId={Id}", questionId);

        return success;
    }

    private static QuizQuestionResponseDto MapToDto(QuizQuestion q) => new()
    {
        QuestionId    = q.QuestionId,
        QuizId        = q.QuizId,
        QuestionType  = q.QuestionType,
        QuestionText  = q.QuestionText,
        OptionA       = q.OptionA,
        OptionB       = q.OptionB,
        OptionC       = q.OptionC,
        OptionD       = q.OptionD,
        CorrectOption = q.CorrectOption,
        Difficulty    = q.Difficulty,
        Explanation   = q.Explanation,
        OrderIndex    = q.OrderIndex,
        IsActive      = q.IsActive,
        CreatedAt     = q.CreatedAt
    };

    /// <summary>
    /// Maps to the student-safe DTO — <c>CorrectOption</c> and <c>Explanation</c>
    /// are intentionally excluded to prevent cheating.
    /// </summary>
    private static StudentQuizQuestionResponseDto MapToStudentDto(QuizQuestion q) => new()
    {
        QuestionId   = q.QuestionId,
        QuestionType = q.QuestionType,
        QuestionText = q.QuestionText,
        OptionA      = q.OptionA,
        OptionB      = q.OptionB,
        OptionC      = q.OptionC,
        OptionD      = q.OptionD,
        Difficulty   = q.Difficulty,
        OrderIndex   = q.OrderIndex
    };
}
