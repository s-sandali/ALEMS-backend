using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business-logic operations for coding question management.
/// </summary>
public interface ICodingQuestionService
{
    /// <summary>
    /// Retrieves all coding questions.
    /// </summary>
    Task<IEnumerable<CodingQuestionResponseDto>> GetAllAsync();

    /// <summary>
    /// Retrieves a single coding question by ID. Returns null if not found.
    /// </summary>
    Task<CodingQuestionResponseDto?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new coding question.
    /// </summary>
    Task<CodingQuestionResponseDto> CreateAsync(CreateCodingQuestionDto dto);

    /// <summary>
    /// Updates an existing coding question's fields.
    /// Throws <see cref="KeyNotFoundException"/> if the ID does not exist.
    /// </summary>
    Task<CodingQuestionResponseDto> UpdateAsync(int id, UpdateCodingQuestionDto dto);

    /// <summary>
    /// Deletes a coding question by ID.
    /// Returns false if not found.
    /// </summary>
    Task<bool> DeleteAsync(int id);
}
