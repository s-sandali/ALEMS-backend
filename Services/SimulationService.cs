using backend.Models.Simulations;
using backend.Services.Simulations;

namespace backend.Services;

/// <summary>
/// Coordinates algorithm simulation execution.
/// </summary>
public class SimulationService : ISimulationService
{
    private readonly IEnumerable<IAlgorithmSimulationEngine> _engines;
    private readonly ILogger<SimulationService> _logger;

    public SimulationService(
        IEnumerable<IAlgorithmSimulationEngine> engines,
        ILogger<SimulationService> logger)
    {
        _engines = engines;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<SimulationResponse> RunAsync(string algorithm, int[] array)
    {
        var engine = ResolveEngine(algorithm);
        var clonedInput = array.ToArray();
        return Task.FromResult(engine.Run(clonedInput));
    }

    /// <inheritdoc />
    public Task<SimulationValidationResponse> ValidateStepAsync(
        string algorithm,
        int[] currentArray,
        string actionType,
        int[] indices)
    {
        var engine = ResolveEngine(algorithm);

        return Task.FromResult(engine.ValidateStep(
            currentArray.ToArray(),
            actionType,
            indices.ToArray()));
    }

    private IAlgorithmSimulationEngine ResolveEngine(string algorithm)
    {
        var normalizedAlgorithm = algorithm.Trim().ToLowerInvariant();
        var engine = _engines.FirstOrDefault(e => e.CanHandle(normalizedAlgorithm));
        if (engine is null)
        {
            _logger.LogWarning("Simulation requested for unsupported algorithm {Algorithm}", algorithm);
            throw new NotSupportedException($"Algorithm '{algorithm}' is not supported.");
        }

        return engine;
    }
}
