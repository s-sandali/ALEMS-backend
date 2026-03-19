namespace backend.Models.Simulations;

/// <summary>
/// Stores the step trace and learner progress for an interactive practice session.
/// </summary>
public class SimulationSession
{
    public string SessionId { get; set; } = string.Empty;

    public List<SimulationStep> Steps { get; set; } = [];

    public int CurrentStepIndex { get; set; }
}
