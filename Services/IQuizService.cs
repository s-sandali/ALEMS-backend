using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business-logic operations for quiz management.
/// </summary>
public interface IQuizService
{
    /// <summary>
    /// Retrieves all quizzes (active and inactive). Admin use only.
    /// </summary>
    Task<IEnumerable<QuizResponseDto>> GetAllQuizzesAsync();

    /// <summary>
    /// Retrieves only active quizzes (is_active = true). Student-facing.
    /// </summary>
    Task<IEnumerable<QuizResponseDto>> GetActiveQuizzesAsync();

    /// <summary>
    /// Retrieves a single quiz by ID. Returns null if not found.
    /// </summary>
    Task<QuizResponseDto?> GetQuizByIdAsync(int id);

    /// <summary>
    /// Retrieves a single active quiz by ID. Returns null if not found or inactive.
    /// Student-facing — inactive quizzes are treated as not found.
    /// </summary>
    Task<QuizResponseDto?> GetActiveQuizByIdAsync(int id);

    /// <summary>
    /// Creates a new quiz authored by the user identified by <paramref name="clerkUserId"/>.
    /// Throws <see cref="KeyNotFoundException"/> if the Clerk user has no local record.
    /// Throws <see cref="ArgumentException"/> if the algorithm ID does not exist.
    /// </summary>
    Task<QuizResponseDto> CreateQuizAsync(CreateQuizDto dto, string clerkUserId);

    /// <summary>
    /// Updates an existing quiz's mutable fields.
    /// Throws <see cref="KeyNotFoundException"/> if the quiz ID does not exist.
    /// </summary>
    Task<QuizResponseDto> UpdateQuizAsync(int id, UpdateQuizDto dto);

    /// <summary>
    /// Soft-deletes a quiz by setting is_active = false.
    /// Returns false if not found.
    /// </summary>
    Task<bool> DeleteQuizAsync(int id);

    /// <summary>
    /// Retrieves statistics for a specific quiz including attempt count, average score, and pass rate.
    /// Returns null if the quiz does not exist.
    /// </summary>
    Task<QuizStatsDto?> GetStatsAsync(int id);
}
