using backend.Models.Simulations;

namespace backend.Services;

/// <summary>
/// Defines operations for algorithm simulation execution.
/// </summary>
public interface ISimulationService
{
    /// <summary>
    /// Runs a simulation for the supplied algorithm and input array.
    /// </summary>
    Task<SimulationResponse> RunAsync(string algorithm, int[] array);
}
