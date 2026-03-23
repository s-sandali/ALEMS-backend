using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request payload for running an algorithm simulation.
/// </summary>
public class RunSimulationRequestDto
{
    [Required]
    public string Algorithm { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public int[] Array { get; set; } = [];

    public int? Target { get; set; }
}
