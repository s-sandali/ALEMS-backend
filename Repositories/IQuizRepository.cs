using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Defines data-access operations for the quizzes table.
/// </summary>
public interface IQuizRepository
{
    /// <summary>
    /// Retrieves all quizzes (active and inactive).
    /// </summary>
    Task<IEnumerable<Quiz>> GetAllAsync();

    /// <summary>
    /// Retrieves a single quiz by its ID, or null if not found.
    /// </summary>
    Task<Quiz?> GetByIdAsync(int id);

    /// <summary>
    /// Inserts a new quiz and returns the created record with the generated ID.
    /// </summary>
    Task<Quiz> CreateAsync(Quiz quiz);

    /// <summary>
    /// Updates an existing quiz's mutable fields.
    /// Returns true if a row was affected, false if not found.
    /// </summary>
    Task<bool> UpdateAsync(Quiz quiz);

    /// <summary>
    /// Soft-deletes a quiz by setting is_active = false.
    /// Returns true if a row was affected, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(int id);
}
