using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Handles business logic for quiz CRUD operations.
/// </summary>
public class QuizService : IQuizService
{
    private readonly IQuizRepository     _quizRepository;
    private readonly IUserRepository     _userRepository;
    private readonly IAlgorithmRepository _algorithmRepository;
    private readonly ILogger<QuizService> _logger;

    public QuizService(
        IQuizRepository quizRepository,
        IUserRepository userRepository,
        IAlgorithmRepository algorithmRepository,
        ILogger<QuizService> logger)
    {
        _quizRepository      = quizRepository;
        _userRepository      = userRepository;
        _algorithmRepository = algorithmRepository;
        _logger              = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuizResponseDto>> GetAllQuizzesAsync()
    {
        var quizzes = await _quizRepository.GetAllAsync();
        return quizzes.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QuizResponseDto>> GetActiveQuizzesAsync()
    {
        var quizzes = await _quizRepository.GetActiveAsync();
        return quizzes.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<QuizResponseDto?> GetQuizByIdAsync(int id)
    {
        var quiz = await _quizRepository.GetByIdAsync(id);
        return quiz is not null ? MapToDto(quiz) : null;
    }

    /// <inheritdoc />
    public async Task<QuizResponseDto?> GetActiveQuizByIdAsync(int id)
    {
        var quiz = await _quizRepository.GetActiveByIdAsync(id);
        return quiz is not null ? MapToDto(quiz) : null;
    }

    /// <inheritdoc />
    public async Task<QuizResponseDto> CreateQuizAsync(CreateQuizDto dto, string clerkUserId)
    {
        // Resolve the admin's internal user ID from their Clerk sub claim
        var creator = await _userRepository.GetByClerkUserIdAsync(clerkUserId);
        if (creator is null)
        {
            _logger.LogWarning("CreateQuiz: no local user record for ClerkId={ClerkId}", clerkUserId);
            throw new KeyNotFoundException(
                "Authenticated user does not have a local account. Complete user sync first.");
        }

        // Validate that the referenced algorithm exists
        var algorithm = await _algorithmRepository.GetByIdAsync(dto.AlgorithmId);
        if (algorithm is null)
        {
            _logger.LogWarning("CreateQuiz: algorithm not found — AlgorithmId={Id}", dto.AlgorithmId);
            throw new ArgumentException($"Algorithm with ID {dto.AlgorithmId} does not exist.");
        }

        var quiz = new Quiz
        {
            AlgorithmId   = dto.AlgorithmId,
            CreatedBy     = creator.UserId,
            Title         = dto.Title,
            Description   = dto.Description,
            TimeLimitMins = dto.TimeLimitMins,
            PassScore     = dto.PassScore,
            IsActive      = true
        };

        var created = await _quizRepository.CreateAsync(quiz);
        _logger.LogInformation(
            "CreateQuiz: QuizId={QuizId} created by UserId={UserId}", created.QuizId, creator.UserId);

        return MapToDto(created);
    }

    /// <inheritdoc />
    public async Task<QuizResponseDto> UpdateQuizAsync(int id, UpdateQuizDto dto)
    {
        var existing = await _quizRepository.GetByIdAsync(id);
        if (existing is null)
        {
            _logger.LogWarning("UpdateQuiz: quiz not found — QuizId={Id}", id);
            throw new KeyNotFoundException($"Quiz with ID {id} was not found.");
        }

        existing.Title         = dto.Title;
        existing.Description   = dto.Description;
        existing.TimeLimitMins = dto.TimeLimitMins;
        existing.PassScore     = dto.PassScore;
        existing.IsActive      = dto.IsActive;

        await _quizRepository.UpdateAsync(existing);
        _logger.LogInformation("UpdateQuiz: QuizId={QuizId} updated", id);

        var updated = await _quizRepository.GetByIdAsync(id);
        return MapToDto(updated!);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteQuizAsync(int id)
    {
        var success = await _quizRepository.DeleteAsync(id);

        if (success)
            _logger.LogInformation("DeleteQuiz: QuizId={QuizId} soft-deleted", id);
        else
            _logger.LogWarning("DeleteQuiz: quiz not found — QuizId={Id}", id);

        return success;
    }

    /// <summary>
    /// Maps a <see cref="Quiz"/> domain model to a <see cref="QuizResponseDto"/>.
    /// </summary>
    private static QuizResponseDto MapToDto(Quiz quiz) => new()
    {
        QuizId        = quiz.QuizId,
        AlgorithmId   = quiz.AlgorithmId,
        CreatedBy     = quiz.CreatedBy,
        Title         = quiz.Title,
        Description   = quiz.Description,
        TimeLimitMins = quiz.TimeLimitMins,
        PassScore     = quiz.PassScore,
        IsActive      = quiz.IsActive,
        CreatedAt     = quiz.CreatedAt,
        UpdatedAt     = quiz.UpdatedAt
    };
}
