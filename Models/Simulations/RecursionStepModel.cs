using System.Text.Json.Serialization;

namespace backend.Models.Simulations;

/// <summary>
/// Recursion metadata attached to each simulation step.
/// </summary>
public class RecursionStepModel
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; set; }

    public int Depth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CurrentFrameId { get; set; }

    public List<RecursionFrameModel> Stack { get; set; } = [];
}

/// <summary>
/// A single frame in the recursion call stack.
/// </summary>
public class RecursionFrameModel
{
    public int Id { get; set; }

    public string FunctionName { get; set; } = string.Empty;

    public int Depth { get; set; }

    public string State { get; set; } = string.Empty;

    public int LeftIndex { get; set; }

    public int RightIndex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReturnValue { get; set; }
}
