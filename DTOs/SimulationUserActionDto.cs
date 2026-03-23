using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Represents the learner's attempted action for the current step.
/// </summary>
public class SimulationUserActionDto
{
    [Required]
    public string Type { get; set; } = string.Empty;

    public int[] Indices { get; set; } = [];
}
