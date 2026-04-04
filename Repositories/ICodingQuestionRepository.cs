using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Defines data-access operations for the coding_questions table.
/// </summary>
public interface ICodingQuestionRepository
{
    /// <summary>
    /// Retrieves all coding questions.
    /// </summary>
    Task<IEnumerable<CodingQuestion>> GetAllAsync();

    /// <summary>
    /// Retrieves a single coding question by its ID, or null if not found.
    /// </summary>
    Task<CodingQuestion?> GetByIdAsync(int id);

    /// <summary>
    /// Inserts a new coding question and returns the created record with the generated ID.
    /// </summary>
    Task<CodingQuestion> CreateAsync(CodingQuestion question);

    /// <summary>
    /// Updates an existing coding question's mutable fields.
    /// Returns true if a row was affected, false if not found.
    /// </summary>
    Task<bool> UpdateAsync(CodingQuestion question);

    /// <summary>
    /// Hard-deletes a coding question by ID.
    /// Returns true if a row was affected, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(int id);
}
