namespace backend.Models.Simulations;

/// <summary>
/// Represents the result of validating a learner's simulation step.
/// </summary>
public class SimulationValidationResponse
{
    public string SessionId { get; set; } = string.Empty;

    public bool Correct { get; set; }

    public int[] NewArrayState { get; set; } = [];

    /// <summary>
    /// Backward-compatible alias for older callers that still read nextState.
    /// </summary>
    public int[] NextState { get; set; } = [];

    public string NextExpectedAction { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Hint { get; set; } = string.Empty;

    public int[] SuggestedIndices { get; set; } = [];

    public int CurrentStepIndex { get; set; }
}
