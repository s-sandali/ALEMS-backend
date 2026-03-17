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

    /// <summary>
    /// Validates whether a learner action is correct for the current algorithm state.
    /// </summary>
    Task<SimulationValidationResponse> ValidateStepAsync(string algorithm, int[] currentArray, string actionType, int[] indices);
}
