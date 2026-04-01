using System.Text.Json.Serialization;

namespace backend.Models.Simulations;

/// <summary>
/// Recursion metadata attached to each simulation step.
/// </summary>
public class RecursionStepModel
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Event { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; set; }

    public int Depth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrentFrameId { get; set; }

    public List<RecursionFrameModel> Stack { get; set; } = [];
}

/// <summary>
/// A single frame in the recursion call stack.
/// </summary>
public class RecursionFrameModel
{
    public string Id { get; set; } = string.Empty;

    public string FunctionName { get; set; } = string.Empty;

    public int Depth { get; set; }

    public string State { get; set; } = string.Empty;

    public int LeftIndex { get; set; }

    public int RightIndex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MidpointIndex { get; set; }

    public RecursionFrameArguments Arguments { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReturnValue { get; set; }
}

/// <summary>
/// Function arguments captured for a recursion frame.
/// </summary>
public class RecursionFrameArguments
{
    public int Left { get; set; }

    public int Right { get; set; }
}
