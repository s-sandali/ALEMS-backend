namespace backend.Models.Simulations;

/// <summary>
/// Represents a single step in an algorithm simulation.
/// </summary>
public class SimulationStep
{
    public int StepNumber { get; set; }

    public int[] ArrayState { get; set; } = [];

    public int[] ActiveIndices { get; set; } = [];

    public int LineNumber { get; set; }

    public string ActionLabel { get; set; } = string.Empty;
}
