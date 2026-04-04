using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Data-access contract for the quiz_questions table.
/// </summary>
public interface IQuizQuestionRepository
{
    /// <summary>Returns all active questions for a quiz, ordered by order_index.</summary>
    Task<IEnumerable<QuizQuestion>> GetByQuizIdAsync(int quizId);

    /// <summary>Returns a single question by its primary key, or null if not found.</summary>
    Task<QuizQuestion?> GetByIdAsync(int questionId);

    /// <summary>Inserts a new question and returns the persisted row.</summary>
    Task<QuizQuestion> CreateAsync(QuizQuestion question);

    /// <summary>Updates all mutable fields. Returns true if a row was affected.</summary>
    Task<bool> UpdateAsync(QuizQuestion question);

    /// <summary>Soft-deletes by setting is_active = false. Returns true if a row was affected.</summary>
    Task<bool> DeleteAsync(int questionId);
}
