using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Defines data-access operations for the algorithms table.
/// </summary>
public interface IAlgorithmRepository
{
    /// <summary>
    /// Retrieves all active algorithms ordered by name.
    /// </summary>
    Task<IEnumerable<Algorithm>> GetAllAsync();

    /// <summary>
    /// Retrieves a single algorithm by its primary key, or null if not found.
    /// </summary>
    Task<Algorithm?> GetByIdAsync(int id);
}
