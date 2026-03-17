using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request payload for validating an interactive simulation step.
/// </summary>
public class ValidateSimulationStepRequestDto
{
    [Required]
    public string Algorithm { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public int[] CurrentArray { get; set; } = [];

    [Required]
    public SimulationUserActionDto UserAction { get; set; } = new();
}
