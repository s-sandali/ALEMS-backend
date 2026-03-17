using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Handles business logic for algorithm retrieval.
/// </summary>
public class AlgorithmService : IAlgorithmService
{
    private readonly IAlgorithmRepository _algorithmRepository;
    private readonly ILogger<AlgorithmService> _logger;

    public AlgorithmService(IAlgorithmRepository algorithmRepository, ILogger<AlgorithmService> logger)
    {
        _algorithmRepository = algorithmRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AlgorithmResponseDto>> GetAllAlgorithmsAsync()
    {
        var algorithms = await _algorithmRepository.GetAllAsync();
        return algorithms.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<AlgorithmResponseDto?> GetAlgorithmByIdAsync(int id)
    {
        var algorithm = await _algorithmRepository.GetByIdAsync(id);

        if (algorithm is null)
        {
            _logger.LogWarning("Algorithm not found — ID={Id}", id);
            return null;
        }

        return MapToDto(algorithm);
    }

    /// <summary>
    /// Maps an <see cref="Algorithm"/> domain model to an <see cref="AlgorithmResponseDto"/>.
    /// </summary>
    private static AlgorithmResponseDto MapToDto(Algorithm algorithm)
    {
        return new AlgorithmResponseDto
        {
            AlgorithmId           = algorithm.AlgorithmId,
            Name                  = algorithm.Name,
            Category              = algorithm.Category,
            Description           = algorithm.Description,
            TimeComplexityBest    = algorithm.TimeComplexityBest,
            TimeComplexityAverage = algorithm.TimeComplexityAverage,
            TimeComplexityWorst   = algorithm.TimeComplexityWorst,
            CreatedAt             = algorithm.CreatedAt
        };
    }
}
