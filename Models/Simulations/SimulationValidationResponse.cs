namespace backend.Models.Simulations;

/// <summary>
/// Represents the result of validating a learner's simulation step.
/// </summary>
public class SimulationValidationResponse
{
    public bool Correct { get; set; }

    public int[] NextState { get; set; } = [];
}
