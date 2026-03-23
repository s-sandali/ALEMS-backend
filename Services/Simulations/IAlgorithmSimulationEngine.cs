using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Defines a simulation engine for a specific algorithm.
/// </summary>
public interface IAlgorithmSimulationEngine
{
    /// <summary>
    /// Returns true when the engine can simulate the requested algorithm key.
    /// </summary>
    bool CanHandle(string algorithm);

    /// <summary>
    /// Executes the simulation and returns the full step trace.
    /// </summary>
    SimulationResponse Run(int[] array, int? targetValue = null);
}
