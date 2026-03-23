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
    Task<SimulationResponse> RunAsync(string algorithm, int[] array, int? targetValue);

    /// <summary>
    /// Starts a stateful practice-mode simulation session.
    /// </summary>
    Task<SimulationSession> StartSessionAsync(string algorithm, int[] array, int? targetValue);

    /// <summary>
    /// Validates whether a learner action is correct for the current practice session.
    /// </summary>
    Task<SimulationValidationResponse> ValidateStepAsync(string sessionId, string actionType, int[] indices);
}
