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
    private readonly HashSet<string> _quizReadyAlgorithmKeys;
    private static readonly string[] DefaultQuizReadyAlgorithms = ["bubble_sort", "binary_search", "heap_sort"];

    public AlgorithmService(
        IAlgorithmRepository algorithmRepository,
        ILogger<AlgorithmService> logger,
        IConfiguration configuration)
    {
        _algorithmRepository = algorithmRepository;
        _logger = logger;
        _quizReadyAlgorithmKeys = configuration
            .GetSection("Quiz:ReadyAlgorithms")
            .Get<string[]>()?
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet() ?? DefaultQuizReadyAlgorithms.ToHashSet();

        if (_quizReadyAlgorithmKeys.Count == 0)
        {
            _quizReadyAlgorithmKeys = DefaultQuizReadyAlgorithms.ToHashSet();
        }
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
    private AlgorithmResponseDto MapToDto(Algorithm algorithm)
    {
        var algorithmKey = algorithm.Name
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_");

        return new AlgorithmResponseDto
        {
            AlgorithmId           = algorithm.AlgorithmId,
            Name                  = algorithm.Name,
            Category              = algorithm.Category,
            Description           = algorithm.Description,
            TimeComplexityBest    = algorithm.TimeComplexityBest,
            TimeComplexityAverage = algorithm.TimeComplexityAverage,
            TimeComplexityWorst   = algorithm.TimeComplexityWorst,
            QuizAvailable         = _quizReadyAlgorithmKeys.Contains(algorithmKey),
            CreatedAt             = algorithm.CreatedAt
        };
    }
}
