namespace backend.Models.Simulations;

/// <summary>
/// Represents the full response payload for an algorithm simulation.
/// </summary>
public class SimulationResponse
{
    public string AlgorithmName { get; set; } = string.Empty;

    public List<SimulationStep> Steps { get; set; } = [];

    public int TotalSteps { get; set; }

    public int? TargetValue { get; set; }
}
