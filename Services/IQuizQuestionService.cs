using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business-logic operations for quiz question management.
/// </summary>
public interface IQuizQuestionService
{
    /// <summary>
    /// Returns all active questions for the given quiz, ordered by order_index.
    /// Throws <see cref="KeyNotFoundException"/> if the quiz does not exist.
    /// </summary>
    Task<IEnumerable<QuizQuestionResponseDto>> GetByQuizIdAsync(int quizId);

    /// <summary>
    /// Returns a single question by ID, or null if not found.
    /// </summary>
    Task<QuizQuestionResponseDto?> GetByIdAsync(int questionId);

    /// <summary>
    /// Creates a new question under the specified quiz.
    /// Throws <see cref="KeyNotFoundException"/> if the quiz does not exist.
    /// </summary>
    Task<QuizQuestionResponseDto> CreateAsync(int quizId, CreateQuizQuestionDto dto);

    /// <summary>
    /// Updates all mutable fields of an existing question.
    /// Throws <see cref="KeyNotFoundException"/> if the question does not exist.
    /// </summary>
    Task<QuizQuestionResponseDto> UpdateAsync(int questionId, UpdateQuizQuestionDto dto);

    /// <summary>
    /// Soft-deletes a question by setting is_active = false.
    /// Returns false if not found.
    /// </summary>
    Task<bool> DeleteAsync(int questionId);
}
