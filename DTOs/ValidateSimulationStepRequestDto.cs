using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.DTOs;

/// <summary>
/// Request payload for validating an interactive simulation step.
/// </summary>
public class ValidateSimulationStepRequestDto
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Legacy request field retained for compatibility with earlier callers.
    /// </summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>
    /// Legacy request field retained for compatibility with earlier callers.
    /// </summary>
    public int[]? CurrentArray { get; set; }

    /// <summary>
    /// Preferred request field used by the frontend practice-mode flow.
    /// </summary>
    public SimulationUserActionDto? Action { get; set; }

    /// <summary>
    /// Legacy request field retained for compatibility with earlier callers.
    /// </summary>
    public SimulationUserActionDto? UserAction { get; set; }

    [JsonIgnore]
    public SimulationUserActionDto? ResolvedAction => Action ?? UserAction;
}
