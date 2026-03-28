using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business-logic operations for quiz management.
/// </summary>
public interface IQuizService
{
    /// <summary>
    /// Retrieves all quizzes (active and inactive).
    /// </summary>
    Task<IEnumerable<QuizResponseDto>> GetAllQuizzesAsync();

    /// <summary>
    /// Retrieves a single quiz by ID. Returns null if not found.
    /// </summary>
    Task<QuizResponseDto?> GetQuizByIdAsync(int id);

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
}
