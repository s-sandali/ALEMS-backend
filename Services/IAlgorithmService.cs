using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business-logic operations for algorithm management.
/// </summary>
public interface IAlgorithmService
{
    /// <summary>
    /// Retrieves all algorithms.
    /// </summary>
    Task<IEnumerable<AlgorithmResponseDto>> GetAllAlgorithmsAsync();

    /// <summary>
    /// Retrieves a single algorithm by ID. Returns null if not found.
    /// </summary>
    Task<AlgorithmResponseDto?> GetAlgorithmByIdAsync(int id);
}
